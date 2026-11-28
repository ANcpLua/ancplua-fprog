"""Offline release decisions; these tests never modify Git refs or submit a package."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import release as r


SHA = "ab" * 20
OTHER = "cd" * 20
DIGEST = "af" * 32
VERSION = "0.7.1"
MARK = f"Release notes\n<!-- nuget-accepted source={SHA} payload-sha256={DIGEST} -->"


class ReleaseTests(unittest.TestCase):
    def stub(self, name, **kwargs):
        stub = patch.object(r, name, **kwargs)
        mock = stub.start()
        self.addCleanup(stub.stop)
        return mock

    def test_only_deliberate_main_or_release_tag_publishes(self):
        for ref, event, trailers, result in (
                ("refs/heads/main", "push", [VERSION], VERSION),
                ("refs/tags/v" + VERSION, "push", [], VERSION),
                ("refs/tags/v" + VERSION, "workflow_dispatch", [], VERSION),
                ("refs/heads/main", "push", [], ""),
                ("refs/heads/ongoing", "push", [VERSION], ""),
                ("refs/pull/2/merge", "pull_request", [VERSION], "")):
            with self.subTest(ref=ref, event=event, trailers=trailers):
                self.assertEqual(r.release_intent(ref, event, trailers, VERSION), result)

    def test_mismatched_ambiguous_and_unstable_intent_fail(self):
        for markers in ([VERSION, VERSION], ["0.7.0"], ["0.7.1-beta"], ["00.7.1"]):
            with self.subTest(markers=markers), self.assertRaises(ValueError):
                r.release_intent("refs/heads/main", "push", markers, VERSION)

    def test_checkout_credentials_do_not_reach_consumer_or_pack(self):
        with patch.dict(os.environ, {"GH_TOKEN": "x", "ACTIONS_ID_TOKEN_REQUEST_TOKEN": "x",
                                     "NUGET_API_KEY": "x", "DOTNET_NOLOGO": "1"}, clear=True):
            self.assertEqual(r.build_env(), {"DOTNET_NOLOGO": "1"})

    def test_acceptance_receipt_survives_unindexed_rerun(self):
        self.stub("release_body", return_value=MARK)
        indexed = self.stub("indexed")
        tag = self.stub("reserve")
        self.assertFalse(r.preflight(VERSION, SHA, DIGEST))
        indexed.assert_not_called()
        tag.assert_called_once_with(VERSION, SHA)

    def test_already_indexed_package_requires_live_consumer_equivalence(self):
        self.stub("release_body", return_value=None)
        self.stub("indexed", return_value=True)
        verify = self.stub("verify_public")
        self.stub("reserve")
        self.assertFalse(r.preflight(VERSION, SHA, DIGEST))
        verify.assert_called_once_with(VERSION, SHA, DIGEST)

    def test_indexed_collision_cannot_reserve_a_tag_or_publish(self):
        self.stub("release_body", return_value=None)
        self.stub("indexed", return_value=True)
        self.stub("verify_public", side_effect=ValueError("package mismatch"))
        tag = self.stub("reserve")
        with self.assertRaisesRegex(ValueError, "package mismatch"):
            r.preflight(VERSION, OTHER, DIGEST)
        tag.assert_not_called()

    def test_conflicting_receipt_stops(self):
        self.stub("release_body", return_value=MARK)
        tag = self.stub("reserve")
        with self.assertRaisesRegex(ValueError, "differs"):
            r.preflight(VERSION, OTHER, DIGEST)
        tag.assert_not_called()

    def test_tag_collision_and_superseded_main_are_not_overwritten(self):
        self.stub("version_at", return_value=VERSION)
        self.stub("run")
        reads = self.stub("read")
        tag = self.stub("remote_tag")
        with patch("subprocess.run") as write:
            reads.return_value = SHA
            tag.return_value = OTHER
            with self.assertRaisesRegex(ValueError, "another commit"):
                r.reserve(VERSION, SHA)
            tag.return_value = None
            reads.side_effect = [SHA, OTHER]
            with self.assertRaisesRegex(ValueError, "superseded"):
                r.reserve(VERSION, SHA)
            write.assert_not_called()

    def test_lost_push_response_reconciles_only_verified_bytes(self):
        index = self.stub("indexed", return_value=True)
        verify = self.stub("verify_public")
        with patch.dict(os.environ, {"NUGET_API_KEY": "test-only-key"}), \
                patch("subprocess.run", return_value=subprocess.CompletedProcess([], 1, "HTTP 409")):
            r.push_nuget("test.nupkg", VERSION, SHA, DIGEST)
            index.assert_called_once_with(VERSION, wait_seconds=60)
            verify.assert_called_once_with(VERSION, SHA, DIGEST)
            index.return_value = False
            with self.assertRaisesRegex(RuntimeError, "did not confirm acceptance"):
                r.push_nuget("test.nupkg", VERSION, SHA, DIGEST)

    def test_verified_handoff_selects_immutable_release_harness(self):
        self.stub("version_at", return_value=VERSION)
        self.stub("remote_tag", return_value=SHA)
        self.stub("release_body", return_value=MARK)
        self.stub("indexed", return_value=True)
        commands = self.stub("run")
        outputs = self.stub("output")
        with patch("subprocess.run", return_value=subprocess.CompletedProcess([], 0)):
            r.prepare_indexed(None, SHA)
        commands.assert_any_call("git", "checkout", "--quiet", "--detach", SHA)
        outputs.assert_any_call("digest", DIGEST)

    def test_manual_legacy_check_uses_main_only_when_tag_has_no_harness(self):
        self.stub("remote_tag", return_value=SHA)
        self.stub("release_body", return_value="Historical release.")
        self.stub("indexed", return_value=True)
        commands = self.stub("run")
        self.stub("output")
        with patch("subprocess.run", return_value=subprocess.CompletedProcess([], 1)):
            r.prepare_indexed(VERSION, None)
        commands.assert_called_once_with("git", "merge-base", "--is-ancestor", SHA, "origin/main")

    def test_release_notes_only_take_named_released_section(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(r, "ROOT", Path(tmp)):
            (Path(tmp) / "CHANGELOG.md").write_text("## 0.7.2 (unreleased)\n\nWIP\n\n## 0.7.1\n\nChange.\n\n## 0.7.0\n\nOlder.\n")
            self.assertEqual(r.notes(VERSION), "Change.")
            with self.assertRaises(ValueError):
                r.notes("0.7.2")


if __name__ == "__main__":
    unittest.main()
