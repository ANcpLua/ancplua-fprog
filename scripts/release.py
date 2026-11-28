"""Pack and exercise exact analyzer packages, and reconcile an intentional CI release."""
import argparse
from contextlib import closing
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time
from urllib.error import HTTPError, URLError
from urllib.request import urlopen
from xml.etree import ElementTree


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = "ANcpLua.Fprog.Analyzers"
PROJECT = f"{PACKAGE}/{PACKAGE}.csproj"
LEGACY_PROJECT = "ancplua-fprog/ancplua-fprog.csproj"
REPO = "ANcpLua/ancplua-fprog"
PLAYGROUND = ROOT / "ANcpLua.Fprog.Analyzers.Playground" / "verify.py"
INDEX = "https://api.nuget.org/v3-flatcontainer/ancplua.fprog.analyzers/index.json"
STABLE = re.compile(r"(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)")
PAYLOAD = re.compile(r"[0-9a-f]{64}")
COMMIT = re.compile(r"[0-9a-f]{40}")
STAMP = re.compile(r"<!-- nuget-accepted source=([0-9a-f]{40}) payload-sha256=([0-9a-f]{64}) -->")


def read(*args):
    return subprocess.check_output(args, cwd=ROOT, text=True).strip()


def run(*args, env=None):
    subprocess.run(args, cwd=ROOT, env=env, check=True)


def build_env():
    # Package builds and consumer code have no role in authorizing the upload or tag.
    excluded = {"GH_TOKEN", "GITHUB_TOKEN", "NUGET_API_KEY", "ACTIONS_ID_TOKEN_REQUEST_TOKEN",
                "ACTIONS_ID_TOKEN_REQUEST_URL", "ACTIONS_RUNTIME_TOKEN"}
    return {name: value for name, value in os.environ.items() if name not in excluded}


def output(key, value):
    if file := os.environ.get("GITHUB_OUTPUT"):
        with open(file, "a", encoding="utf-8") as out:
            print(f"{key}={value}", file=out)
    print(f"{key}={value}")


def version_at(revision):
    paths = read("git", "ls-tree", "-r", "--name-only", revision, "--", PROJECT, LEGACY_PROJECT).splitlines()
    if len(paths) != 1:
        raise ValueError(f"Expected one analyzer project at {revision}, found {paths}")
    xml = read("git", "show", f"{revision}:{paths[0]}")
    return ElementTree.fromstring(xml).findtext(".//VersionPrefix")


def notes(version):
    text = (ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
    entry = re.search(rf"^## {re.escape(version)}[ \t]*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
    if not entry or not entry[1].strip():
        raise ValueError(f"Prepare a released changelog section headed '## {version}'")
    return entry[1].strip()


def release_intent(ref, event, trailers, expected):
    if event not in ("push", "workflow_dispatch"):
        return ""
    if ref.startswith("refs/tags/v"):
        version = ref.removeprefix("refs/tags/v")
    elif ref == "refs/heads/main" and trailers:
        if len(trailers) != 1:
            raise ValueError("Expected exactly one NuGet-Release trailer")
        version = trailers[0]
    else:
        return ""
    if not STABLE.fullmatch(version) or version != expected:
        raise ValueError(f"Release intent '{version}' must match stable VersionPrefix '{expected}'")
    return version


def intent(ref, event):
    trailers = read("git", "log", "-1", "--format=%(trailers:key=NuGet-Release,valueonly)").splitlines()
    version = release_intent(ref, event, trailers, version_at("HEAD"))
    if version:
        notes(version)
    return version


def gate(directory, version):
    if not re.fullmatch(STABLE.pattern + r"(?:-ci\.\d+)?", version):
        raise ValueError(f"Unexpected package version: {version}")
    env = build_env()
    package_dir = directory / "package"
    run("dotnet", "pack", PROJECT, "-c", "Release", "-warnaserror", "--disable-build-servers",
        "--artifacts-path", str(directory / "build"), "--output", str(package_dir), f"-p:Version={version}", env=env)
    candidate = package_dir / f"{PACKAGE}.{version}.nupkg"
    # Stream the individual green/red checks and consume the digest only after a successful exit.
    command = [sys.executable, "-B", str(PLAYGROUND), "--package", str(candidate),
               "--expected-commit", read("git", "rev-parse", "HEAD")]
    with subprocess.Popen(command, cwd=ROOT, env=env, text=True, stdout=subprocess.PIPE) as consumer:
        digest = None
        for line in consumer.stdout:
            print(line, end="", flush=True)
            if line.startswith("PLAYGROUND_PAYLOAD_SHA256="):
                digest = line.partition("=")[2].strip()
        if consumer.wait():
            raise RuntimeError("The analyzer package failed the Playground")
    if not digest or not PAYLOAD.fullmatch(digest):
        raise ValueError("No verified package digest was returned")
    return candidate, digest


def local(release):
    sha = read("git", "rev-parse", "HEAD")
    version = ElementTree.parse(ROOT / PROJECT).findtext(".//VersionPrefix")
    if release:
        if read("git", "branch", "--show-current") != "main" or read("git", "status", "--porcelain"):
            raise ValueError("Commit the intended release on a clean local main before running make release")
        origin = read("git", "remote", "get-url", "--push", "origin").lower().removesuffix(".git")
        if origin not in {"https://github.com/" + REPO.lower(), "git@github.com:" + REPO.lower(),
                          "ssh://git@github.com/" + REPO.lower()}:
            raise ValueError("origin must be the analyzer's canonical repository")
        if intent("refs/heads/main", "push") != version:
            raise ValueError(f"Add 'NuGet-Release: {version}' as a trailer to the local release commit")
    with tempfile.TemporaryDirectory(prefix="fprog-playground-pack-") as temp:
        gate(Path(temp), version)
    if release:
        if read("git", "rev-parse", "HEAD") != sha or read("git", "status", "--porcelain"):
            raise ValueError("Checkout changed during the local release gate")
        run("git", "push", "origin", sha + ":refs/heads/main")


def remote_tag(tag):
    ref = "refs/tags/" + tag
    refs = {line.split()[1]: line.split()[0] for line in
            read("git", "ls-remote", "origin", ref, ref + "^{}").splitlines()}
    return refs.get(ref + "^{}", refs.get(ref))


def reserve(version, sha):
    if not STABLE.fullmatch(version) or not COMMIT.fullmatch(sha) or version_at("HEAD") != version:
        raise ValueError("Release version and source SHA must identify the checked candidate")
    if read("git", "rev-parse", "HEAD") != sha:
        raise ValueError("The checkout differs from the release commit")
    tag = "v" + version
    run("git", "fetch", "--quiet", "--no-tags", "origin", "refs/heads/main:refs/remotes/origin/main")
    run("git", "merge-base", "--is-ancestor", sha, "refs/remotes/origin/main")
    target = remote_tag(tag)
    if target == sha:
        return
    if target:
        raise ValueError(f"{tag} already belongs to another commit")
    if read("git", "rev-parse", "refs/remotes/origin/main") != sha:
        raise ValueError("This untagged release was superseded on main; prepare the intended tip")
    # Never move, delete or force an existing release tag.
    proc = subprocess.run(["gh", "api", "--method", "POST", f"repos/{REPO}/git/refs",
                           "-f", "ref=refs/tags/" + tag, "-f", "sha=" + sha], cwd=ROOT,
                          stdout=subprocess.DEVNULL)
    if remote_tag(tag) != sha:
        raise RuntimeError(f"Could not reserve {tag} at {sha} (GitHub exit {proc.returncode})")


def release_body(version):
    # Listing by tag in a successful API response distinguishes absence from auth or transport failure.
    query = ("query($tag:String!){repository(owner:\"ANcpLua\",name:\"ancplua-fprog\")"
             "{release(tagName:$tag){description isDraft tagName}}}")
    data = json.loads(read("gh", "api", "graphql", "-f", "query=" + query, "-f", "tag=v" + version))
    release = data["data"]["repository"]["release"]
    if release and (release["tagName"] != "v" + version or release["isDraft"]):
        raise ValueError("Resolve the existing inconsistent or draft GitHub release first")
    return None if release is None else (release["description"] or "")


def stamp(body):
    marks = STAMP.findall(body or "")
    if len(marks) > 1 or ("<!-- nuget-accepted" in (body or "") and not marks):
        raise ValueError("Invalid acceptance receipt in the GitHub release notes")
    return marks[0] if marks else None


def indexed(version, wait_seconds=0):
    deadline = time.monotonic() + wait_seconds
    delay = 5
    while True:
        try:
            with closing(urlopen(INDEX, timeout=20)) as response:
                if version in json.load(response)["versions"]:
                    return True
        except HTTPError as error:
            if error.code != 404 and not (wait_seconds and (error.code == 429 or 500 <= error.code < 600)):
                raise
        except (URLError, TimeoutError):
            if not wait_seconds:
                raise
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return False
        time.sleep(min(delay, remaining))
        delay = min(delay * 2, 60)


def verify_public(version, sha, digest=None):
    args = [sys.executable, "-B", str(PLAYGROUND), "--nuget", version, "--expected-commit", sha]
    if digest:
        args.extend(["--expected-payload-sha256", digest])
    run(*args, env=build_env())


def preflight(version, sha, digest):
    if not PAYLOAD.fullmatch(digest):
        raise ValueError("Expected the Playground's verified payload digest")
    body = release_body(version)
    accepted = stamp(body)
    if accepted:
        if accepted != (sha, digest):
            raise ValueError("Existing acceptance receipt differs from this package")
        reserve(version, sha)
        return False
    if indexed(version):
        verify_public(version, sha, digest)
        reserve(version, sha)
        return False
    if body is not None:
        raise ValueError("Existing GitHub release lacks a receipt and the package is not indexed; retry after indexing")
    reserve(version, sha)
    return True


def push_nuget(candidate, version, sha, digest):
    key = os.environ["NUGET_API_KEY"]
    command = ["dotnet", "nuget", "push", candidate, "--api-key", key,
               "--source", "https://api.nuget.org/v3/index.json"]
    pushed = subprocess.run(command, cwd=ROOT, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    print(pushed.stdout.replace(key, "***"))
    if pushed.returncode:
        # A conflict or lost HTTP response can follow a successful submission.
        # Never turn a duplicate response into success without checking the public bytes.
        if not indexed(version, wait_seconds=60):
            raise RuntimeError("NuGet push did not confirm acceptance; inspect the failure and rerun after indexing")
        verify_public(version, sha, digest)


def finish(version, sha, digest):
    if remote_tag("v" + version) != sha:
        raise ValueError("Release tag changed before recording acceptance")
    body = release_body(version)
    accepted = stamp(body)
    if accepted and accepted != (sha, digest):
        raise ValueError("Conflicting release receipt")
    if accepted:
        return
    note = f"<!-- nuget-accepted source={sha} payload-sha256={digest} -->"
    with tempfile.TemporaryDirectory(prefix="fprog-release-notes-") as temp:
        path = Path(temp, "notes.md")
        path.write_text((body if body is not None else notes(version)) + "\n\n" + note + "\n", encoding="utf-8")
        if body is None:
            run("gh", "release", "create", "v" + version, "--repo", REPO, "--verify-tag",
                "--title", PACKAGE + " " + version, "--notes-file", str(path))
        else:
            run("gh", "release", "edit", "v" + version, "--repo", REPO, "--notes-file", str(path))


def prepare_indexed(version, sha):
    if sha:
        if not COMMIT.fullmatch(sha):
            raise ValueError("Invalid release commit")
        version = version_at(sha)
    if not version or not STABLE.fullmatch(version):
        raise ValueError("Verify an exact stable package version")
    tagged = remote_tag("v" + version)
    if not tagged or (sha and sha != tagged):
        raise ValueError("Expected the verified release tag at the requested commit")
    run("git", "merge-base", "--is-ancestor", tagged, "origin/main")
    body = release_body(version)
    if body is None:
        raise ValueError("The tagged release has no recorded GitHub release")
    accepted = stamp(body)
    if sha and not accepted:
        raise ValueError("Missing acceptance receipt for an automated release")
    if accepted and accepted[0] != tagged:
        raise ValueError("The acceptance receipt belongs to another commit")
    if not indexed(version, wait_seconds=1200):
        raise RuntimeError("Package not indexed after 20 minutes; retry Verify published package without publishing")
    historical = subprocess.run(["git", "cat-file", "-e", f"{tagged}:ANcpLua.Fprog.Analyzers.Playground/verify.py"],
                                cwd=ROOT, stderr=subprocess.DEVNULL).returncode == 0
    if historical:
        run("git", "checkout", "--quiet", "--detach", tagged)
    elif sha:
        raise ValueError("Automated acceptance requires the release commit's own Playground")
    else:
        print("Historical tag predates the Playground; using the current main contract")
    output("version", version)
    output("commit", tagged)
    output("digest", accepted[1] if accepted else "")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("intent")
    commands.add_parser("local").add_argument("--push", action="store_true")
    pack = commands.add_parser("pack")
    pack.add_argument("--output", type=Path, required=True)
    pack.add_argument("--version", required=True)
    for name in ("preflight", "push", "finish"):
        cmd = commands.add_parser(name)
        cmd.add_argument("--version", required=True)
        cmd.add_argument("--sha", required=True)
        cmd.add_argument("--digest", required=True)
        if name == "push":
            cmd.add_argument("--candidate", required=True)
    post = commands.add_parser("prepare-indexed")
    post.add_argument("--version")
    post.add_argument("--sha")
    args = parser.parse_args()
    if args.command == "intent":
        output("version", intent(os.environ["GITHUB_REF"], os.environ["GITHUB_EVENT_NAME"]))
    elif args.command == "local":
        local(args.push)
    elif args.command == "pack":
        candidate, digest = gate(args.output.resolve(), args.version)
        output("candidate", candidate)
        output("digest", digest)
    elif args.command == "preflight":
        output("should_push", str(preflight(args.version, args.sha, args.digest)).lower())
    elif args.command == "push":
        push_nuget(args.candidate, args.version, args.sha, args.digest)
    elif args.command == "finish":
        finish(args.version, args.sha, args.digest)
    else:
        prepare_indexed(args.version, args.sha)


if __name__ == "__main__":
    main()
