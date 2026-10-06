const number = new Intl.NumberFormat('zh-CN');

function setText(id, value) {
  const element = document.querySelector(`#${id}`);
  if (element) element.textContent = value;
}

function renderHistory(history) {
  const chart = document.querySelector('#history-chart');
  const empty = document.querySelector('#history-empty');
  const recent = history.slice(-30);

  if (!chart || recent.length < 2) {
    if (empty) empty.hidden = false;
    return;
  }

  const values = recent.map((entry) => entry.installerDownloads);
  const minimum = Math.min(...values);
  const maximum = Math.max(...values);
  const range = Math.max(maximum - minimum, 1);

  recent.forEach((entry, index) => {
    const bar = document.createElement('span');
    const height = 14 + ((entry.installerDownloads - minimum) / range) * 86;
    bar.style.height = `${height}%`;
    bar.title = `${entry.date}：${number.format(entry.installerDownloads)} 次`;
    bar.setAttribute('aria-label', bar.title);
    if (index === recent.length - 1) bar.classList.add('current');
    chart.appendChild(bar);
  });
}

fetch('data/downloads.json', { cache: 'no-store' })
  .then((response) => {
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  })
  .then((data) => {
    setText('installer-downloads', number.format(data.totals.installerDownloads));
    setText('package-downloads', number.format(data.totals.packageDownloads));
    setText('release-count', number.format(data.releaseCount));

    if (data.latestRelease) {
      setText('latest-downloads', number.format(data.latestRelease.installerDownloads));
      setText('latest-version', `${data.latestRelease.tagName} 安装包`);
    }

    if (data.updatedAt) {
      const updated = new Date(data.updatedAt).toLocaleString('zh-CN', {
        timeZone: 'Asia/Shanghai',
        dateStyle: 'medium',
        timeStyle: 'short',
      });
      setText('updated-at', `数据更新于 ${updated}`);
    } else {
      setText('updated-at', '首次自动更新尚未运行');
    }

    renderHistory(data.history || []);
  })
  .catch(() => {
    setText('updated-at', '数据暂时无法载入，请稍后再试');
    const empty = document.querySelector('#history-empty');
    if (empty) {
      empty.hidden = false;
      empty.textContent = '数据暂时无法载入，请稍后再试。';
    }
  });
