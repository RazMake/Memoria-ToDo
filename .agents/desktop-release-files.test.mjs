import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { latestDesktopRelease } from './desktop-release-files.mjs';
import { createGitHubRuns } from './github-runs.mjs';

describe('desktop delta release discovery (unit)', () => {
  const release = (tag_name, overrides = {}) => ({
    tag_name, draft: false, prerelease: false,
    assets: [{ name: 'releases.win.json' }, { name: `${tag_name}-full.nupkg` }, { name: 'api-client.nupkg' }],
    ...overrides,
  });


  describe('reading paginated releases (unit)', () => {
    test('reads every GitHub release page without using the latest-release endpoint', async () => {
      const github = createGitHubRuns(async (file, args) => {
        assert.equal(file, 'gh');
        assert.ok(args.includes('--paginate'));
        assert.ok(args.includes('--slurp'));
        assert.ok(args.includes('repos/example/tasks/releases?per_page=100'));
        return JSON.stringify([[{ tag_name: 'android-v1.0.0' }], [{ tag_name: 'v1.0.0' }]]);
      }, { repository: 'example/tasks' });
      assert.deepEqual(await github.releases(), [{ tag_name: 'android-v1.0.0' }, { tag_name: 'v1.0.0' }]);
    });
    test('rejects malformed release pages', async () => {
      const github = createGitHubRuns(async () => '{"message":"Unavailable"}', { repository: 'example/tasks' });
      await assert.rejects(github.releases(), /invalid.*release/i);
    });
  });
  test('finds the highest desktop version beyond more than one page of Android releases', () => {
    const releases = Array.from({ length: 101 }, (_, i) => release(`android-v1.0.${i}`));
    releases.push(release('v1.0.0'), release('v2.0.0'), release('v3.0.0', { draft: true }), release('v4.0.0', { prerelease: true }));
    const latest = latestDesktopRelease(releases);
    assert.equal(latest.tag, 'v2.0.0');
    assert.deepEqual(latest.assets, ['releases.win.json', 'v2.0.0-full.nupkg']);
  });
  test('reports no earlier desktop release when only Android releases exist', () => {
    assert.equal(latestDesktopRelease([release('android-v9.0.0')]), undefined);
  });
  test('rejects malformed GitHub data instead of treating it as a first release', () => {
    assert.throws(() => latestDesktopRelease([{ tag_name: 'v1.0.0' }]));
  });
});
