const menuButton = document.querySelector('.menu-button');
const navLinks = document.querySelector('.nav-links');
const languageButton = document.querySelector('.language-button');

const english = new Map(Object.entries({
  '跳到主要内容': 'Skip to main content',
  '功能': 'Features', '隐私': 'Privacy', '常见问题': 'FAQ', '支持作者': 'Support', '社区': 'Community',
  '下载 Windows 版': 'Download for Windows', '本地优先 · Windows': 'Local-first · Windows',
  '让每一份工作，': 'Keep a clear record', '都有迹可循。': 'of the work you do.',
  '添加项目文件夹，工迹会在后台安静记录有效变化，自动整理今天做了什么、何时做的，以及改动过哪些文件。': 'Add a project folder and WorkDelta quietly records meaningful changes, organizing what you did, when you did it, and which files changed.',
  '免费下载': 'Free download', '适用于 Windows 10 / 11': 'For Windows 10 / 11',
  '无需账号': 'No account', '无需联网': 'Works offline', '免费使用': 'Free to use',
  '工作有迹，变化可见': 'Keep your work visible', '本地记录中': 'Recording locally',
  '跟踪的项目': 'Tracked projects', '＋ 添加项目文件夹': '+ Add project folder', '作品集网站': 'Portfolio website',
  '● 数据只保存在本机': '● Data stays on this PC', '正在跟踪': 'Tracking',
  '今日项目活动': 'Today’s activity', '3小时 42分': '3h 42m', '涉及文件': 'Files touched', '有效变化': 'Effective changes',
  '今日工作时间线': 'Today’s work timeline', '按 15 分钟无活动自动分段': 'New session after 15 minutes inactive',
  '完善项目备份和恢复能力': 'Improve project backup and restore', '优化文件变化过滤规则': 'Improve file-change filtering',
  'PathPolicy.cs · 4 个测试': 'PathPolicy.cs · 4 tests', '次有效变化': 'effective changes', '今日记录已保存': 'Today’s activity saved',
  '不是监控软件。': 'Not surveillance software. ', '只记录项目文件的变化时间。': 'It only records when project files change.',
  '本地运行': 'Local operation', '项数据上传': 'items uploaded', '个账号要求': 'accounts required',
  '核心功能': 'Core features', '不打断工作，': 'Stay focused.', '也不会忘记工作。': 'Remember what you did.',
  '工迹在后台把零散的文件变化，整理成一条真正看得懂的工作记录。': 'WorkDelta turns scattered file changes into a work record you can actually understand.',
  '自动生成工作时间线': 'Automatic work timeline',
  '编辑、保存、切换文件时无需手动点击计时。相近的变化会合并成工作时段，15 分钟无活动后自动分段。': 'No timer to start when you edit, save, or switch files. Nearby changes are grouped into sessions, with a new session after 15 minutes of inactivity.',
  '搭建首页结构': 'Build the homepage structure', '1 小时 18 分': '1h 18m', '调整响应式样式': 'Tune responsive styles', '42 分钟': '42m',
  '完善发布流程': 'Improve the release workflow', '1 小时 02 分': '1h 02m', '一键导出日报': 'Export daily reports',
  '把今天的项目、时段和改动文件整理成中文 Markdown，复盘和汇报都更轻松。': 'Turn today’s projects, sessions, and changed files into a localized Markdown report.',
  '内容检查点': 'Content checkpoints', '在隔离的本地历史空间中保留文本内容检查点，不会修改你的项目文件。': 'Keep text-content checkpoints in isolated local history without modifying your project files.',
  '安静驻留后台': 'Quietly runs in the background', '关闭窗口后继续在系统托盘运行，也可以选择开机自动启动。': 'Close the window and WorkDelta keeps running in the tray, with optional startup launch.',
  '隐私边界': 'Privacy boundaries', '你的工作，': 'Your work stays', '只属于你。': 'yours.',
  '工迹不需要账号，不读取真实邮箱，也不把项目内容发送到网络。活动数据库和历史检查点全部保存在你的 Windows 本机。': 'WorkDelta needs no account, does not read your real email, and never sends project contents over the network. Activity data and checkpoints stay on your Windows PC.',
  '不记录键盘': 'No keystroke logging', '只观察你主动添加的项目文件夹中的文件变化。': 'Only watches file changes inside project folders you explicitly add.',
  '不上传内容': 'No content uploads', '无需网络服务，数据默认留在本地应用目录。': 'No network service is required; data remains in the local app directory.',
  '边界清晰可控': 'Clear, controllable scope', '只处理你主动添加的项目文件夹，记录范围始终由你决定。': 'Only processes folders you add, so you always control the recording scope.',
  '使用方式': 'How it works', '三步，找回今天的工作。': 'Recover your day in three steps.',
  '添加项目': 'Add a project', '拖入或选择需要跟踪的项目文件夹。': 'Drop or choose a project folder to track.',
  '专注工作': 'Focus on your work', '工迹在托盘中安静整理有效文件变化。': 'WorkDelta quietly organizes meaningful file changes in the tray.',
  '查看与导出': 'Review and export', '打开时间线，或者导出今天的工作日报。': 'Open the timeline or export today’s work report.',
  '开始之前，': 'Before you start,', '你可能想知道。': 'you may want to know.',
  '工迹会监控我所有的电脑操作吗？': 'Does WorkDelta monitor everything I do?',
  '不会。它只观察你主动添加的项目文件夹，不记录键盘、鼠标、应用使用情况或网页浏览记录。': 'No. It only watches folders you explicitly add and does not record keystrokes, mouse activity, app usage, or browsing history.',
  '它会修改我的项目文件吗？': 'Will it modify my project files?',
  '不会。内容检查点保存在独立的本地历史空间中，不会改写你原来的项目文件。': 'No. Checkpoints are stored in separate local history and never rewrite the original project files.',
  '工作时间是如何计算的？': 'How is work time calculated?',
  '工迹根据有效文件变化推断项目活动时间。相近变化会归入同一时段，超过 15 分钟无活动则开始新的时段。它适合回顾工作，不等同于考勤计时。': 'WorkDelta estimates project activity from meaningful file changes. Nearby changes belong to one session; a new session starts after 15 inactive minutes. It is for reviewing work, not attendance tracking.',
  '目前支持哪些系统？': 'Which systems are supported?', '当前版本面向 64 位 Windows 10 和 Windows 11，应用界面使用原生 WPF 构建。': 'The current release supports 64-bit Windows 10 and Windows 11 with a native WPF interface.',
  '发现问题或想建议功能怎么办？': 'How can I report an issue or suggest a feature?', '欢迎前往社区交流使用经验、反馈问题和提出功能想法。': 'Visit the community to share feedback, report issues, and suggest features.',
  '让本地优先的软件，': 'Help local-first software', '继续安静地成长。': 'keep growing quietly.',
  'WorkDelta 的全部功能均可免费使用。如果它对你有帮助，可以自愿支持后续开发；支持不会解锁商品、订阅或额外功能。': 'Every WorkDelta feature is free. If it helps you, you can voluntarily support continued development; supporting does not unlock products, subscriptions, or additional features.',
  '支付宝': 'Alipay', '微信支付': 'WeChat Pay',
  '使用支付宝扫码支持': 'Scan with Alipay', '使用微信扫码支持': 'Scan with WeChat',
  '扫码或在浏览器中打开，支持金额由你填写。': 'Scan the QR code or open PayPal in your browser. You choose the amount.',
  '放大二维码': 'Enlarge QR code', '打开 PayPal': 'Open PayPal',
  '从今天开始': 'Start today', '别再靠记忆，': 'Stop relying on memory', '回想今天做了什么。': 'to reconstruct your day.',
  '免费下载工迹，让每一次有效变化都有迹可循。': 'Download WorkDelta for free and keep every meaningful change visible.',
  '进入社区': 'Visit the community ', '让每一份工作，都有迹可循。': 'Keep a clear record of the work you do.',
  '产品': 'Product', '下载': 'Download', '交流与反馈': 'Discussion & feedback', '功能建议': 'Feature requests',
  '免费 · 本地优先 · 为专注工作而做': 'Free · Local-first · Built for focused work'
}));

const originalText = new WeakMap();
function textNodes(root) {
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const nodes = [];
  while (walker.nextNode()) nodes.push(walker.currentNode);
  return nodes;
}

function setLanguage(language) {
  const useEnglish = language === 'en';
  document.documentElement.lang = useEnglish ? 'en' : 'zh-CN';
  for (const node of textNodes(document.body)) {
    if (!originalText.has(node)) originalText.set(node, node.nodeValue);
    const source = originalText.get(node);
    const trimmed = source.trim();
    if (!trimmed) continue;
    const translated = english.get(trimmed);
    node.nodeValue = useEnglish && translated
      ? source.replace(trimmed, translated)
      : source;
  }

  document.querySelectorAll('.brand > span:last-child').forEach((node) => {
    node.innerHTML = useEnglish ? '<strong>WorkDelta</strong>' : '工迹 <strong>WorkDelta</strong>';
  });
  document.querySelector('.app-brand > span:last-child > b').textContent = useEnglish ? 'WorkDelta' : '工迹 WorkDelta';
  languageButton.textContent = useEnglish ? '中文' : 'EN';
  languageButton.setAttribute('aria-label', useEnglish ? 'Switch to Chinese' : '切换到英文');
  document.querySelectorAll('.brand').forEach((node) => node.setAttribute('aria-label', useEnglish ? 'WorkDelta home' : '工迹 WorkDelta 首页'));
  document.querySelector('.nav-links')?.setAttribute('aria-label', useEnglish ? 'Main navigation' : '主导航');
  document.querySelector('.menu-button')?.setAttribute('aria-label', useEnglish ? 'Open navigation' : '打开导航');
  document.querySelector('.hero-notes')?.setAttribute('aria-label', useEnglish ? 'Product highlights' : '产品特点');
  document.querySelector('.product-stage')?.setAttribute('aria-label', useEnglish ? 'WorkDelta app preview' : '工迹应用界面预览');
  const paymentImages = document.querySelectorAll('.payment-card img');
  const paymentFrames = document.querySelectorAll('.payment-qr-frame');
  if (paymentImages.length === 3 && paymentFrames.length === 3) {
    const imageLabels = useEnglish
      ? ['Alipay payment QR code', 'WeChat Pay payment QR code', 'PayPal payment QR code']
      : ['支付宝收款二维码', '微信收款二维码', 'PayPal 付款二维码'];
    const frameLabels = useEnglish
      ? ['Enlarge the Alipay payment QR code', 'Enlarge the WeChat Pay payment QR code', 'Open the PayPal support page']
      : ['放大支付宝收款二维码', '放大微信收款二维码', '打开 PayPal 支持页面'];
    paymentImages.forEach((node, index) => node.setAttribute('alt', imageLabels[index]));
    paymentFrames.forEach((node, index) => node.setAttribute('aria-label', frameLabels[index]));
  }

  document.title = useEnglish ? 'WorkDelta | Local-first work history for Windows' : '工迹 WorkDelta｜本地优先的工作记录工具';
  const description = useEnglish
    ? 'WorkDelta is a local-first Windows app that turns meaningful file changes into a clear work timeline.'
    : '工迹 WorkDelta 是一款本地优先的 Windows 工作记录工具，自动把文件变化整理成清晰的工作时间线。';
  document.querySelector('meta[name="description"]').content = description;
  document.querySelector('meta[property="og:title"]').content = useEnglish ? 'WorkDelta | Keep your work visible' : '工迹 WorkDelta｜让每一份工作，都有迹可循';
  document.querySelector('meta[property="og:description"]').content = description;
  try { localStorage.setItem('workdelta-language', language); } catch {}
}

menuButton?.addEventListener('click', () => {
  const isOpen = navLinks.classList.toggle('open');
  menuButton.setAttribute('aria-expanded', String(isOpen));
});

navLinks?.addEventListener('click', () => {
  navLinks.classList.remove('open');
  menuButton?.setAttribute('aria-expanded', 'false');
});

let currentLanguage;
try { currentLanguage = localStorage.getItem('workdelta-language'); } catch {}
if (!currentLanguage) currentLanguage = navigator.language.toLowerCase().startsWith('zh') ? 'zh' : 'en';
setLanguage(currentLanguage);

languageButton?.addEventListener('click', () => {
  currentLanguage = document.documentElement.lang === 'en' ? 'zh' : 'en';
  setLanguage(currentLanguage);
});

document.querySelector('#year').textContent = new Date().getFullYear();
