DOTNET ?= dotnet
JQ ?= jq
JB ?= $(DOTNET) tool run jb
REF ?= $(DOTNET) tool run ref

SLN := ancplua-fprog.slnx
ANALYZER_PROJ := ancplua-fprog/ancplua-fprog.csproj
ANALYZER_OUT := artifacts/analyzer
ANALYZER_DLL := $(ANALYZER_OUT)/ancplua-fprog.dll
PACKAGE_OUT := artifacts/package

.DEFAULT_GOAL := help
.PHONY: help tools build test inspect verify-dll check pack manifest

help: ## List the targets
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*## "}; {printf "  %-12s %s\n", $$1, $$2}'

tools: ## Restore the pinned local tools (jb, ref)
	$(DOTNET) tool restore

build: ## Build the solution with warnings as errors
	@mkdir -p artifacts
	$(DOTNET) build $(SLN) -c Release -warnaserror "-bl:artifacts/build-{}.binlog"

test: ## Run all tests
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

pack: ## Pack the analyzer NuGet package into artifacts/package
	$(DOTNET) pack $(ANALYZER_PROJ) -c Release -warnaserror -o $(PACKAGE_OUT)

manifest: ## Record commit, SDK and tool versions for this run
	@set -eu; mkdir -p artifacts; { \
	  echo "commit: $$(git rev-parse HEAD)"; \
	  echo "sdk: $$($(DOTNET) --version)"; \
	  date -u +"utc: %Y-%m-%dT%H:%M:%SZ"; \
	  $(DOTNET) tool list --local; \
	} > artifacts/run-manifest.txt; cat artifacts/run-manifest.txt
