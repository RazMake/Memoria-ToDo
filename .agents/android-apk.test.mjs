import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { publishApk, validateApk, validateUploadedApk } from './android-apk.mjs';

describe('publishing an Android APK (unit)', () => {
  const certificate = 'a'.repeat(64);
  const expected = { applicationId: 'example.tasks', version: '1.2.3', versionCode: '1002003', abi: 'arm64-v8a', certificate };
  const good = {
    size: 12,
    sha256: 'c'.repeat(64),
    badging: "package: name='example.tasks' versionCode='1002003' versionName='1.2.3'\nnative-code: 'arm64-v8a'\n",
    signature: `Signer #1 certificate SHA-256 digest: ${certificate}\n`,
  };
  const fixture = (inspection = good) => {
    const calls = [];
    return {
      calls,
      inspect: async () => inspection,
      createDraft: async () => calls.push('draft'),
      upload: async () => calls.push('upload'),
      verifyUpload: async () => {},
      publish: async () => calls.push('public'),
      write: () => {},
    };
  };

  test('uploads the validated APK to a draft before making it public', async () => {
    const ports = fixture();
    await publishApk('app.apk', expected, ports);
    assert.deepEqual(ports.calls, ['draft', 'upload', 'public']);
  });

  describe('the uploaded APK (unit)', () => {
    const expected = { tag: 'android-v1.2.3', name: 'app.apk', size: 12, sha256: 'c'.repeat(64) };
    const asset = { name: expected.name, size: expected.size, digest: `sha256:${expected.sha256}`, state: 'uploaded' };
    const release = { tag_name: expected.tag, draft: true, prerelease: true, assets: [asset] };
    test('accepts the exact completed APK attached to its prerelease draft', () => validateUploadedApk(release, expected));
    for (const [reason, changes] of [
      ['an unfinished transfer', { assets: [{ ...asset, state: 'new' }] }],
      ['different bytes', { assets: [{ ...asset, digest: `sha256:${'d'.repeat(64)}` }] }],
      ['a truncated APK', { assets: [{ ...asset, size: 11 }] }],
      ['a wrong tag', { tag_name: 'android-v1.2.4' }],
      ['a missing asset', { assets: [] }],
    ]) {
      test(`rejects ${reason}`, () => assert.throws(() => validateUploadedApk({ ...release, ...changes }, expected)));
    }
  });

  for (const [reason, changes] of [
    ['empty APK', { size: 0 }],
    ['unidentified APK contents', { sha256: undefined }],
    ['wrong application', { badging: good.badging.replace('example.tasks', 'another.app') }],
    ['wrong display version', { badging: good.badging.replace("versionName='1.2.3'", "versionName='1.2.4'") }],
    ['wrong version code', { badging: good.badging.replace('1002003', '1002004') }],
    ['wrong ABI', { badging: good.badging.replace('arm64-v8a', 'x86_64') }],
    ['extra ABI', { badging: good.badging.replace("'arm64-v8a'", "'arm64-v8a' 'x86_64'") }],
    ['wrong certificate', { signature: good.signature.replace(certificate, 'b'.repeat(64)) }],
    ['missing signature', { signature: '' }],
  ]) {
    test(`rejects an ${reason} before creating or uploading a release`, async () => {
      const ports = fixture({ ...good, ...changes });
      await assert.rejects(publishApk('app.apk', expected, ports));
      assert.deepEqual(ports.calls, []);
    });
  }

  test('does not make a release public when its upload fails', async () => {
    const ports = fixture();
    ports.upload = async () => { throw new Error('Upload interrupted'); };
    await assert.rejects(publishApk('app.apk', expected, ports), /Upload interrupted/);
    assert.deepEqual(ports.calls, ['draft']);
  });

  test('does not make a release public when its uploaded asset is incomplete', async () => {
    const ports = fixture();
    ports.verifyUpload = async () => { throw new Error('The uploaded APK is incomplete'); };
    await assert.rejects(publishApk('app.apk', expected, ports), /incomplete/);
    assert.deepEqual(ports.calls, ['draft', 'upload']);
  });

  test('accepts the release certificate independent of hex case', () => {
    validateApk({ ...good, signature: good.signature.toUpperCase().replace('SIGNER', 'Signer').replace('CERTIFICATE SHA-256 DIGEST', 'certificate SHA-256 digest') }, expected);
  });

  test('accepts the APK v3 certificate reported by the pinned build tools', () => {
    validateApk({ ...good, signature: `Number of signers: 1\nV3.0 Signer: certificate SHA-256 digest: ${certificate}\n` }, expected);
  });
});
