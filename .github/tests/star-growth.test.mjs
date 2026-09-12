import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

// Execute the actual workflow shell with a fake gh function; no network or posts.
const workflow = readFileSync(new URL('../workflows/star-growth.yml', import.meta.url), 'utf8')
const runBlock = workflow.split(/        run: \|\r?\n/)[1]
assert.ok(runBlock, 'Expected the digest run block')
const digestScript = runBlock.replace(/^          /gm, '').replace(/\r\n/g, '\n')
const bash = process.env.UDT_TEST_BASH ?? (process.platform === 'win32'
  ? join(process.env.ProgramFiles ?? 'C:/Program Files', 'Git/bin/bash.exe')
  : 'bash')

const fakeGitHub = String.raw`
gh() {
  if [ "$1" = api ]; then
    if [[ "$*" == *--slurp* && "$*" == *--jq* ]]; then
      echo 'gh does not support --slurp with --jq' >&2
      return 1
    fi
    case "$*" in
      *search/issues*) [ "$MOCK_EXISTING" = 0 ] || echo 158 ;;
      *issues/158/comments*)
        if [ "$MOCK_FAIL_COMMENTS" = 1 ]; then return 1; fi
        if [ "$MOCK_COMMENTS" = 1 ]; then
          case "$*" in
            *--paginate*) echo "<!-- udt-stars=$MOCK_PREVIOUS -->" ;;
            *) echo '<!-- udt-stars=18 -->' ;;
          esac
        fi ;;
      *issues/158*) echo "<!-- udt-stars=$MOCK_PREVIOUS -->" ;;
      *.stargazers_count*) echo "$MOCK_STARS" ;;
      *.subscribers_count*) echo 0 ;;
      *.watchers_count*) echo "$MOCK_STARS" ;;
      *.forks*) echo 1 ;;
      *.tag_name*) echo v6.1.1 ;;
      *) echo "Unexpected API: $*" >&2; return 1 ;;
    esac
  elif [ "$1" = issue ]; then
    echo "POST_METHOD:$2"
    cat "$RUNNER_TEMP/udt-star-digest.md"
  else
    echo "Unexpected gh call: $*" >&2
    return 1
  fi
}
`

function runDigest(overrides = {}) {
  const directory = mkdtempSync(join(tmpdir(), 'udt-star-growth-test-'))
  const scriptPath = join(directory, 'digest.sh')
  try {
    writeFileSync(scriptPath, fakeGitHub + digestScript, 'utf8')
    return execFileSync(bash, [scriptPath.replaceAll('\\', '/')], {
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
      env: {
        ...process.env,
        REPO: 'test/udt',
        RUNNER_TEMP: directory.replaceAll('\\', '/'),
        MOCK_STARS: '30',
        MOCK_PREVIOUS: '28',
        MOCK_EXISTING: '1',
        MOCK_COMMENTS: '1',
        MOCK_FAIL_COMMENTS: '0',
        ...overrides
      }
    })
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}

test('reports the current campaign, actual watchers and last comment page', () => {
  const result = runDigest()
  assert.match(result, /POST_METHOD:comment/)
  assert.match(result, /Stars:\*\* `30` \(\+2 since last digest\)/)
  assert.match(result, /watching `0`/)
  assert.match(result, /Progress to 100 stars:\*\* 30%.*Remaining:\*\* 70/)
  assert.match(result, /Bilibili and V2EX/)
  assert.match(result, /HelloGitHub #3383/)
})

test('uses the original issue baseline, including zero, before the first comment', () => {
  assert.match(runDigest({ MOCK_COMMENTS: '0', MOCK_PREVIOUS: '0' }), /\+30 since last digest/)
})

test('reports a loss of stars', () => {
  assert.match(runDigest({ MOCK_PREVIOUS: '35' }), /-5 since last digest/)
})

test('creates the tracker when there is no existing issue', () => {
  const result = runDigest({ MOCK_EXISTING: '0', MOCK_STARS: '0' })
  assert.match(result, /POST_METHOD:create/)
  assert.match(result, /Progress to 100 stars:\*\* 0%.*Remaining:\*\* 100/)
  assert.doesNotMatch(result, /since last digest/)
})

for (const stars of ['100', '125']) {
  test(`completes the campaign at ${stars} without exceeding 100% or raising the target`, () => {
    const result = runDigest({ MOCK_STARS: stars })
    assert.match(result, /Progress to 100 stars:\*\* 100%.*Remaining:\*\* 0/)
    assert.match(result, /100-star goal reached/)
    assert.doesNotMatch(result, /Progress to 1,?000|Phase [123]/)
  })
}

test('does not publish a digest when comment history cannot be read', () => {
  assert.throws(() => runDigest({ MOCK_FAIL_COMMENTS: '1' }), error => {
    assert.equal(error.status, 1)
    assert.doesNotMatch(error.stdout, /POST_METHOD:/)
    return true
  })
})
