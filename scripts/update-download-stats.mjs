import { readFile, writeFile } from 'node:fs/promises';
import process from 'node:process';

const repository = process.env.GITHUB_REPOSITORY || 'work-delta/workdelta';
const outputPath = process.env.STATS_OUTPUT || 'site/data/downloads.json';
const fixturePath = process.env.RELEASES_FILE;

async function getReleases() {
  if (fixturePath) {
    return JSON.parse(await readFile(fixturePath, 'utf8'));
  }

  const releases = [];
  const headers = {
    Accept: 'application/vnd.github+json',
    'User-Agent': 'workdelta-download-stats',
    'X-GitHub-Api-Version': '2022-11-28',
  };

  if (process.env.GITHUB_TOKEN) {
    headers.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`;
  }

  for (let page = 1; ; page += 1) {
    const response = await fetch(
      `https://api.github.com/repos/${repository}/releases?per_page=100&page=${page}`,
      { headers },
    );

    if (!response.ok) {
      throw new Error(`GitHub releases request failed: ${response.status} ${response.statusText}`);
    }

    const batch = await response.json();
    releases.push(...batch);
    if (batch.length < 100) break;
  }

  return releases;
}

function packageType(name) {
  const lowerName = name.toLowerCase();
  if (lowerName.endsWith('.exe') || lowerName.endsWith('.msi')) return 'installer';
  if (lowerName.endsWith('.zip')) return 'portable';
  return null;
}

function summarize(releases) {
  const published = releases
    .filter((release) => !release.draft)
    .sort((a, b) => new Date(b.published_at || b.created_at) - new Date(a.published_at || a.created_at));

  const summarizeAssets = (assets = []) => assets.reduce(
    (totals, asset) => {
      const type = packageType(asset.name || '');
      if (!type) return totals;
      const downloads = Number(asset.download_count) || 0;
      totals.packageDownloads += downloads;
      if (type === 'installer') totals.installerDownloads += downloads;
      if (type === 'portable') totals.portableDownloads += downloads;
      return totals;
    },
    { installerDownloads: 0, portableDownloads: 0, packageDownloads: 0 },
  );

  const totals = published.reduce((sum, release) => {
    const current = summarizeAssets(release.assets);
    sum.installerDownloads += current.installerDownloads;
    sum.portableDownloads += current.portableDownloads;
    sum.packageDownloads += current.packageDownloads;
    return sum;
  }, { installerDownloads: 0, portableDownloads: 0, packageDownloads: 0 });

  const latest = published.find((release) => !release.prerelease) || published[0];
  const latestTotals = summarizeAssets(latest?.assets);

  return {
    releaseCount: published.length,
    totals,
    latestRelease: latest ? {
      tagName: latest.tag_name,
      publishedAt: latest.published_at || latest.created_at,
      ...latestTotals,
    } : null,
  };
}

function dateInShanghai(date = new Date()) {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Shanghai',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(date);
}

async function readExisting() {
  try {
    const contents = await readFile(outputPath, 'utf8');
    return contents.trim() ? JSON.parse(contents) : { history: [] };
  } catch (error) {
    if (error.code === 'ENOENT') return { history: [] };
    throw error;
  }
}

const releases = await getReleases();
const summary = summarize(releases);
const existing = await readExisting();
const today = dateInShanghai();
const history = Array.isArray(existing.history)
  ? existing.history.filter((entry) => entry.date !== today)
  : [];

history.push({
  date: today,
  installerDownloads: summary.totals.installerDownloads,
  packageDownloads: summary.totals.packageDownloads,
});

const result = {
  schemaVersion: 1,
  repository,
  updatedAt: new Date().toISOString(),
  ...summary,
  history: history.slice(-730),
};

await writeFile(outputPath, `${JSON.stringify(result, null, 2)}\n`, 'utf8');
console.log(`Recorded ${result.totals.installerDownloads} installer downloads across ${result.releaseCount} releases.`);
