#!/bin/bash
# Analyzes and tests the editor.
set -uo pipefail
cd "$(dirname "$0")/.."

failures=0

# No very long file. Nothing is over the limit any more, so there is no list
# of exceptions to keep: the next file to reach it will be a new one, and it
# fails here rather than growing the way editor_shell.dart grew to 3,646.
LIMIT=1000
over=$(find lib test -name '*.dart' | xargs wc -l | awk -v l="$LIMIT" \
         '$1 > l && $2 != "total" {print "  FAIL  " $2 " is " $1 " lines, over " l}')
if [ -z "$over" ]; then
  echo "  ok    no source over $LIMIT lines"
else
  echo "$over"
  failures=$((failures+1))
fi

if flutter analyze > /tmp/orblit_editor_analyze.log 2>&1; then
  echo "  ok    analyze"
else
  echo "  FAIL  analyze"; tail -20 /tmp/orblit_editor_analyze.log; failures=$((failures+1))
fi

if flutter test > /tmp/orblit_editor_test.log 2>&1; then
  summary=$(tr '\r' '\n' < /tmp/orblit_editor_test.log | tail -1 \
    | sed -e 's/\x1b\[[0-9;]*m//g' -e 's/^[0-9:]* //')
  echo "  ok    $summary"
else
  echo "  FAIL  tests"; tail -25 /tmp/orblit_editor_test.log; failures=$((failures+1))
fi

echo
[ "$failures" -eq 0 ] && echo "everything green" || echo "$failures failing step(s)"
exit "$failures"
