DOTNET ?= dotnet
JQ ?= jq
PYTHON ?= python3
JB ?= $(DOTNET) tool run jb
REF ?= $(DOTNET) tool run ref

SLN := ancplua-fprog.slnx
ANALYZER_PROJ := ancplua-fprog/ancplua-fprog.csproj
ANALYZER_OUT := artifacts/analyzer
ANALYZER_DLL := $(ANALYZER_OUT)/ancplua-fprog.dll

# Referee only: the commit the run started from, and the hidden tests and corpus outside this repo.
BASE ?=
REFEREE ?=
PROTECTED := Makefile mise.toml global.json nuget.config AGENTS.md CLAUDE.md docs/SPEC.source.md \
	.config/dotnet-tools.json tools/feed eng ancplua-fprog.Tests/FpAnalyzerTestHost.cs \
	ancplua-fprog.Tests/Fixtures

.DEFAULT_GOAL := help
.PHONY: help tools build test inspect verify-dll check manifest protected score tools-init

help: ## List the targets
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*## "}; {printf "  %-12s %s\n", $$1, $$2}'

tools: ## Restore the pinned local tools (jb, ref)
	$(DOTNET) tool restore

build: ## Build the solution with warnings as errors
	@mkdir -p artifacts
	$(DOTNET) build $(SLN) -c Release -warnaserror "-bl:artifacts/build-{}.binlog"

test: ## Run all visible tests
	@set -eu; mkdir -p artifacts; results=$$(mktemp -d artifacts/test-results.XXXXXX); \
	$(DOTNET) test --solution $(SLN) -c Release --report-trx --results-directory "$$results" "-bl:artifacts/test-{}.binlog"

inspect: tools ## ReSharper InspectCode; fails on any warning or error
	@set -eu; mkdir -p artifacts; \
	$(JB) inspectcode $(SLN) --swea --severity=WARNING --output=artifacts/inspectcode.sarif; \
	count=$$($(JQ) '[.runs[].results[]] | length' artifacts/inspectcode.sarif); \
	echo "InspectCode findings: $$count"; test "$$count" -eq 0

verify-dll: tools ## Check the analyzer binary with RefTrace
	@set -eu; mkdir -p $(ANALYZER_OUT); \
	$(DOTNET) build $(ANALYZER_PROJ) -c Release -warnaserror -o $(ANALYZER_OUT); \
	$(REF) check $(ANALYZER_PROJ) --forbid-package 'Microsoft.CodeAnalysis.Workspaces*'; \
	$(REF) reflection $(ANALYZER_DLL) --fail-on-findings; \
	$(REF) assembly $(ANALYZER_DLL) --json > artifacts/analyzer-assembly.json

check: build test inspect verify-dll ## The done gate: everything must pass
	@echo "check passed"

manifest: ## Record commit, SDK and tool versions for this run
	@set -eu; mkdir -p artifacts; { \
	  echo "commit: $$(git rev-parse HEAD)"; \
	  echo "sdk: $$($(DOTNET) --version)"; \
	  date -u +"utc: %Y-%m-%dT%H:%M:%SZ"; \
	  $(DOTNET) tool list --local; \
	} > artifacts/run-manifest.txt; cat artifacts/run-manifest.txt

protected: ## Referee: fail if protected files changed since BASE
	@test -n "$(BASE)" || { echo "Set BASE=<commit the run started from>"; exit 2; }
	@git diff --quiet $(BASE) -- $(PROTECTED) || \
	  { echo "Protected files changed:"; git diff --stat $(BASE) -- $(PROTECTED); exit 1; }
	@echo "protected files unchanged"

score: protected ## Referee: hidden tests and clean corpus, scored per rule
	@test -n "$(REFEREE)" || { echo "Set REFEREE=<path with HiddenTests/ and Corpus/>"; exit 2; }
	@set -u; mkdir -p artifacts/score; rm -rf artifacts/score/*; mkdir -p artifacts/score/corpus-sarif; \
	export FprogRepo="$(CURDIR)" FprogCorpusSarifDir="$(CURDIR)/artifacts/score/corpus-sarif"; \
	$(DOTNET) test --project "$(REFEREE)/HiddenTests/HiddenTests.csproj" -c Release \
	  --report-trx --results-directory "$(CURDIR)/artifacts/score/trx"; \
	$(DOTNET) build "$(REFEREE)/Corpus/Corpus.slnx" -c Release "-bl:artifacts/score/corpus.binlog"; \
	$(PYTHON) eng/score.py artifacts/score/trx artifacts/score/corpus-sarif

tools-init: ## Referee, once: create the tool manifest from tools/feed and nuget.org
	$(DOTNET) new tool-manifest --force
	$(DOTNET) tool install --local JetBrains.ReSharper.GlobalTools
	$(DOTNET) tool install --local RefTrace --version 0.9.0-preview.1
