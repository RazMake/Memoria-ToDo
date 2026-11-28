import definitions from './release-platforms.json' with { type: 'json' };

const escapePattern = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const platforms = new Map(Object.entries(definitions.platforms).map(([name, definition]) => [
  name, Object.freeze({ ...definition, name, tagPattern: new RegExp(`^${escapePattern(definition.tagPrefix)}${definitions.versionPattern}$`) }),
]));

export function platformOf(name = 'desktop') {
  const platform = platforms.get(name);
  if (!platform) throw new Error(`Unknown release platform "${name}"; choose ${[...platforms.keys()].join(' or ')}.`);
  return platform;
}

export function validateScriptTests(names) {
  const assigned = new Set([...platforms.values()].flatMap(platform => platform.testScripts));
  const unassigned = names.filter(name => !assigned.has(name));
  if (unassigned.length) throw new Error(`${unassigned.join(', ')}: script tests are not assigned to either release gate.`);
}

export function extractPlatform(args) {
  const remaining = [];
  let name;
  for (let index = 0; index < args.length; index++) {
    if (args[index] !== '--platform') remaining.push(args[index]);
    else {
      if (name !== undefined) throw new Error('--platform is given twice.');
      name = args[++index];
      if (!name || name.startsWith('--')) throw new Error('--platform needs a release platform.');
    }
  }
  return { platform: platformOf(name), args: remaining };
}

export function versionPartsOf(tag, platform = platformOf()) {
  return typeof tag === 'string' ? platform.tagPattern.exec(tag)?.slice(1).map(BigInt) : undefined;
}

export function checkedVersion(tag, platform = platformOf()) {
  const parts = versionPartsOf(tag, platform);
  if (!parts) throw new Error(`"${tag}" is not a version tag; use the form ${platform.tagPrefix}<major>.<minor>.<patch>, such as ${platform.tagPrefix}1.2.3`);
  if (platform.maximumParts && parts.some((part, index) => part > BigInt(platform.maximumParts[index]))) {
    throw new Error(`${tag} exceeds the Android version-code bounds: major <= ${platform.maximumParts[0]}, minor <= ${platform.maximumParts[1]}, patch <= ${platform.maximumParts[2]}.`);
  }
  return parts;
}

export function versionCodeOf(tag, platform) {
  if (!platform.versionCodeWeights) throw new Error(`The ${platform.name} platform has no Android version code.`);
  return checkedVersion(tag, platform).reduce((code, part, index) => code + part * BigInt(platform.versionCodeWeights[index]), 0n).toString();
}
