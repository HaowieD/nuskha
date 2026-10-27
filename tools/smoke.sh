#!/usr/bin/env bash
# End-to-end smoke test against a running instance (see the "smoke" job in .github/workflows/ci.yml).
# Exercises sign-in, status posts, the markup preview, admin pages, book creation, reader submission, approval and rejection.
# Failures are printed as GitHub "::error::" annotations.
set -u
BASE=${BASE:-http://localhost:5000}
ADMIN_LOGIN=${ADMIN_LOGIN:-admin@nuskha.local}
ADMIN_PASSWORD=${ADMIN_PASSWORD:-ChangeMe-Dev-123}
BODY=$(mktemp); fails=0
ADMIN_JAR=$(mktemp); USER_JAR=$(mktemp); ANON_JAR=$(mktemp)
JAR=$ANON_JAR

fail() { echo "::error::SMOKE FAIL: $*"; fails=$((fails + 1)); }
get()  { curl -s -o "$BODY" -w '%{http_code}' -b "$JAR" -c "$JAR" "$BASE$1"; }
expect_get() { local c; c=$(get "$1"); [ "$c" = "${2:-200}" ] || fail "GET $1 -> $c (wanted ${2:-200})"; }
body_has() { grep -qF -- "$1" "$BODY" || fail "$2 (missing: $1)"; }
form_token() { get "$1" >/dev/null; grep -oP 'name="__RequestVerificationToken"[^>]*value="\K[^"]+' "$BODY" | head -1; }
header_token() { grep -oP '"RequestVerificationToken":"\K[^"]+' "$BODY" | head -1; }
post_form() { # path, then curl -d/-F args; prints status code, body in $BODY
  local path=$1; shift
  curl -s -o "$BODY" -w '%{http_code}' -b "$JAR" -c "$JAR" "$@" "$BASE$path"
}

echo "== anonymous pages"
JAR=$ANON_JAR
for p in / /books /genres /genres/fiction /authors /forum /users /account/login /account/register /sitemap.xml /healthz; do expect_get "$p"; done
expect_get /genres/does-not-exist 404
expect_get /tags/does-not-exist 404
expect_get /user/nobody-here 404

echo "== admin sign-in"
JAR=$ADMIN_JAR
t=$(form_token /account/login)
[ -n "$t" ] || fail "no antiforgery token on login page"
c=$(post_form /account/login --data-urlencode "Login=$ADMIN_LOGIN" --data-urlencode "Password=$ADMIN_PASSWORD" --data-urlencode "RememberMe=true" --data-urlencode "__RequestVerificationToken=$t")
[ "$c" = 302 ] || fail "admin login -> $c"

for p in /admin /admin/review /admin/books /admin/books/new /admin/authors /admin/taxonomy /admin/import /admin/reports /admin/users /settings /books/submit /books/mine /notifications /user/admin; do expect_get "$p"; done

echo "== status post and markup"
get / >/dev/null; tok=$(header_token)
[ -n "$tok" ] || fail "no header token on home page"
c=$(post_form /social/status -H "RequestVerificationToken: $tok" -H "HX-Request: true" --data-urlencode 'body=Hello **world** ||secret|| <script>alert(1)</script>')
[ "$c" = 200 ] || fail "status post -> $c"
body_has "<strong>world</strong>" "bold rendered"
body_has "<x-spoiler>secret</x-spoiler>" "spoiler rendered"
grep -q "<script>alert" "$BODY" && fail "raw <script> reached the page"
c=$(post_form /social/preview -H "RequestVerificationToken: $tok" --data-urlencode 'text=*it* [x](javascript:alert(1))')
[ "$c" = 200 ] || fail "preview -> $c"
body_has "<em>it</em>" "preview rendered"
grep -qi 'href="javascript' "$BODY" && fail "javascript: link survived in preview"
expect_get /user/admin; body_has "<strong>world</strong>" "status post visible on profile"

echo "== admin creates a book"
get /admin/books/new >/dev/null
genre=$(grep -oP 'name="Form.GenreIds" value="\K[^"]+' "$BODY" | head -1)
[ -n "$genre" ] || fail "no genres seeded"
t=$(grep -oP 'name="__RequestVerificationToken"[^>]*value="\K[^"]+' "$BODY" | head -1)
c=$(post_form /admin/books/new -F "__RequestVerificationToken=$t" -F "Form.Title=Smoke Test Book" -F "Form.Authors=Smoke Author" \
    -F "Form.Isbn=9780306406157" -F "Form.PublishYear=2020" -F "Form.PageCount=200" -F "Form.Language=ka" -F "Form.Status=1" \
    -F "Form.GenreIds=$genre" -F "Form.Tags=smoke, test run" -F "Form.Description=**Great** book")
[ "$c" = 302 ] || { fail "admin create book -> $c"; grep -oP 'field-error[^<]*<[^<]*|role="alert"[^<]*<[^<]*' "$BODY" | head -3; }
expect_get /books/smoke-test-book; body_has "smoke" "tag shown on book page"; body_has "<strong>Great</strong>" "description markdown"
expect_get /tags/smoke; expect_get /books; body_has "Smoke Test Book" "book in catalog"
expect_get /admin/taxonomy; body_has "test run" "tag in taxonomy admin"

echo "== reader registers and submits books"
JAR=$USER_JAR
t=$(form_token /account/register)
c=$(post_form /account/register --data-urlencode "UserName=tester" --data-urlencode "Email=tester@example.com" --data-urlencode "DisplayName=Tester" \
    --data-urlencode "Password=Passw0rd-123" --data-urlencode "ConfirmPassword=Passw0rd-123" --data-urlencode "__RequestVerificationToken=$t")
[ "$c" = 302 ] || fail "register -> $c"
expect_get /user/tester; expect_get /settings

submit() { # title
  get /books/submit >/dev/null
  local g tk; g=$(grep -oP 'name="Form.GenreIds" value="\K[^"]+' "$BODY" | head -1); tk=$(grep -oP 'name="__RequestVerificationToken"[^>]*value="\K[^"]+' "$BODY" | head -1)
  post_form /books/submit -F "__RequestVerificationToken=$tk" -F "Form.Title=$1" -F "Form.Authors=Reader Author" -F "Form.PublishYear=2019" \
    -F "Form.GenreIds=$g" -F "Form.Tags=reader tag" -F "Form.Description=From a reader"
}
c=$(submit "Tester Submitted Book"); [ "$c" = 200 ] || fail "submit 1 -> $c"; body_has "/books/mine" "submission accepted"
c=$(submit "Tester Second Book");    [ "$c" = 200 ] || fail "submit 2 -> $c"; body_has "/books/mine" "second submission accepted"
c=$(submit "Tester Second Book");    [ "$c" = 200 ] || fail "duplicate submit -> $c"; body_has 'role="alert"' "duplicate refused"
expect_get /books/mine; body_has "Tester Submitted Book" "own submission listed"
expect_get /books/tester-submitted-book 404

echo "== message on another profile"
get /user/admin >/dev/null; tok=$(header_token)
c=$(post_form /social/status -H "RequestVerificationToken: $tok" -H "HX-Request: true" --data-urlencode "to=admin" --data-urlencode "body=Hi admin from tester")
[ "$c" = 200 ] || fail "message on profile -> $c"
expect_get /user/admin; body_has "Hi admin from tester" "message visible on recipient profile"

echo "== staff review"
JAR=$ADMIN_JAR
expect_get /admin/review; body_has "Tester Submitted Book" "pending book in queue"
get /admin/review >/dev/null; t=$(grep -oP 'name="__RequestVerificationToken"[^>]*value="\K[^"]+' "$BODY" | head -1)
# Map titles to ids from the queue page.
approve_id=$(python3 - "$BODY" <<'PY'
import re,sys
html=open(sys.argv[1],encoding='utf-8').read()
for m in re.finditer(r'<h2 id="rv-([0-9a-f-]{36})"[^>]*>([^<]*)</h2>',html):
    if m.group(2).strip()=="Tester Submitted Book": print(m.group(1))
PY
)
reject_id=$(python3 - "$BODY" <<'PY'
import re,sys
html=open(sys.argv[1],encoding='utf-8').read()
for m in re.finditer(r'<h2 id="rv-([0-9a-f-]{36})"[^>]*>([^<]*)</h2>',html):
    if m.group(2).strip()=="Tester Second Book": print(m.group(1))
PY
)
[ -n "$approve_id" ] && [ -n "$reject_id" ] || fail "could not find queue ids"
c=$(post_form "/admin/review?handler=Reject" --data-urlencode "id=$reject_id" --data-urlencode "reason=" --data-urlencode "__RequestVerificationToken=$t")
[ "$c" = 200 ] || fail "reject without reason should re-render (200), got $c"
c=$(post_form "/admin/review?handler=Reject" --data-urlencode "id=$reject_id" --data-urlencode "reason=Needs an ISBN" --data-urlencode "__RequestVerificationToken=$t")
[ "$c" = 302 ] || fail "reject -> $c"
c=$(post_form "/admin/review?handler=Approve" --data-urlencode "id=$approve_id" --data-urlencode "note=Thanks" --data-urlencode "__RequestVerificationToken=$t")
[ "$c" = 302 ] || fail "approve -> $c"

echo "== reader sees the decisions"
JAR=$USER_JAR
expect_get /books/mine; body_has "Needs an ISBN" "rejection reason shown"
expect_get /books/tester-submitted-book; body_has "reader-tag" "approved book has its submitted tag" || true
expect_get /notifications
expect_get /tags/reader-tag

echo "== resubmit after rejection"
get /books/mine >/dev/null
second=$(grep -oP '/books/submit\?edit=\K[0-9a-f-]{36}' "$BODY" | head -1)
[ -n "$second" ] || fail "no edit link for the rejected book"
expect_get "/books/submit?edit=$second"; body_has "Needs an ISBN" "reason repeated on edit page"

echo "== done"
if grep -qE "Unhandled exception|fail: " "${APP_LOG:-/dev/null}" 2>/dev/null; then
  echo "::error::Application log contains errors:"; grep -E -A3 "Unhandled exception|fail: " "$APP_LOG" | head -40 | sed 's/^/::error::/'
  fails=$((fails + 1))
fi
[ "$fails" = 0 ] && echo "smoke test passed" || { echo "$fails check(s) failed"; exit 1; }
