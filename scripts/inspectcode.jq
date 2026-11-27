# InspectCode puts its rules in the SARIF driver. Resolve their severity when a result omits it.
def configured_level($run):
  ($run.tool.driver.rules // []) as $rules
  | (.rule.id // .ruleId) as $id
  | (.rule.index // .ruleIndex // -1) as $given_index
  | (if $given_index >= 0 then $given_index
     else first(range($rules | length) | select($rules[.].id == $id)) // ($rules | length) end) as $index
  | (.provenance.invocationIndex // (if ($run.invocations | length) == 1 then 0 else -1 end)) as $invocation
  | (if $invocation >= 0 then
       first($run.invocations[$invocation].ruleConfigurationOverrides[]?
         | select(($index < ($rules | length) and .descriptor.index == $index)
               or (.descriptor.id != null and .descriptor.id == ($rules[$index].id // $id)))
         | .configuration.level)
     else empty end) // $rules[$index].defaultConfiguration.level // "warning";

def finding($run):
  (if has("level") then .level
   elif (has("kind") | not) or .kind == "fail" then configured_level($run)
   elif .kind | IN("notApplicable", "pass", "review", "open", "informational") then "none"
   else error("Unknown InspectCode result kind: \(.kind)") end) as $level
  | if (["none", "note", "warning", "error"] | index($level)) == null then
      error("Unknown InspectCode severity: \($level)")
    else
      (.message.text // .message.markdown // .message.id // error("Missing InspectCode message")) as $message
      | .locations[0].physicalLocation as $location
      | ([$location.artifactLocation.uri, $location.region.startLine, $location.region.startColumn]
         | map(select(. != null) | tostring) | join(":")) as $where
      | {level: $level, line: ([$where, $level, .ruleId, $message]
          | map(select(. != null and . != "")) | join(" "))}
    end;

if length != 1 or .[0].version != "2.1.0" or (.[0].runs | type != "array" or length == 0) then
  error("Expected one InspectCode SARIF 2.1.0 report with at least one run")
else .[0].runs end
| [.[] as $run
   | if ($run.tool.driver.name | type) != "string" or ($run.results | type) != "array"
        or (($run | has("invocations")) and ($run.invocations | type != "array"))
        or any($run.invocations[]?; .executionSuccessful != true) then
       error("InspectCode did not complete its analysis")
     else $run.results[] | finding($run) end]
| (.[] | .line),
  (group_by(.level) | map({key: .[0].level, value: length}) | from_entries
   | "InspectCode: \(.note // 0) suggestions, \(.warning // 0) warnings, \(.error // 0) errors",
     (if .warning or .error then "" | halt_error(1) else empty end))
