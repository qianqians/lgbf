import { readFileSync } from "node:fs";
import { resolve } from "node:path";

function expectMatch(source, pattern, message) {
  if (!pattern.test(source)) {
    throw new Error(message);
  }
}

function load(relativePath) {
  return readFileSync(resolve(process.cwd(), relativePath), "utf8");
}

function checkAudioManager() {
  const source = load("assets/script/tools/AudioManager/AudioManager.ts");

  expectMatch(source, /audioClips:Map<string,\s*AudioClip>\s*=\s*new Map\(\)/, "AudioManager should cache clips by explicit string key");
  expectMatch(source, /cacheKey:\s*resource/, "AudioManager should use the full bundle\/path as cache key");
  expectMatch(source, /audioSource\s*&&\s*isValid\(this\.audioSource\.node\)/, "AudioManager.Init should be idempotent when an audio node already exists");
  expectMatch(source, /getChildByName\(AudioManager\.audioNodeName\)/, "AudioManager.Init should reuse an existing persistent audio node");
}

function checkUIManager() {
  const source = load("assets/script/tools/UIManager/UIManager.ts");

  expectMatch(source, /pageRequestVersion:number\s*=\s*0/, "UIManager should track page request generations");
  expectMatch(source, /boraderRequestVersion:number\s*=\s*0/, "UIManager should track boarder request generations");
  expectMatch(source, /if\s*\(\s*requestVersion\s*!==\s*this\.pageRequestVersion\s*\)\s*{\s*return;\s*}/, "UIManager.OpenPage should discard stale async page loads");
  expectMatch(source, /OpenBorader failed: current page is unavailable/, "UIManager.OpenBorader should fail fast when no current page exists");
  expectMatch(source, /OpenBorader failed: current page changed while opening/, "UIManager.OpenBorader should reject page-change races");
}

function checkBundleManager() {
  const source = load("assets/script/tools/BundleManager/BundleManager.ts");

  expectMatch(source, /preloadDir\([\s\S]*if\s*\(err\)\s*{[\s\S]*reject\(err\);/, "BundleManager.PreLoadBundleDir should reject preload failures");
  expectMatch(source, /loadRemote\([\s\S]*if\s*\(err\)\s*{[\s\S]*reject\(err\);/, "BundleManager.LoadAssetsFromUrl should propagate remote load errors");
}

try {
  checkAudioManager();
  checkUIManager();
  checkBundleManager();
  console.log("gem/ccc self-check passed.");
} catch (error) {
  console.error("gem/ccc self-check failed:");
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
}
