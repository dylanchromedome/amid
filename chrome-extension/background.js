const AMID_ENDPOINT = "http://127.0.0.1:51234/api/downloads";
const AMID_TIMEOUT_MS = 1200;
const handledDownloadIds = new Set();

chrome.downloads.onCreated.addListener((downloadItem) => {
  handleDownloadCreated(downloadItem);
});

async function handleDownloadCreated(downloadItem) {
  if (handledDownloadIds.has(downloadItem.id)) {
    return;
  }

  const currentItem = await getFreshDownloadItem(downloadItem);
  const url = currentItem.finalUrl || currentItem.url;
  if (!isHttpDownload(url)) {
    return;
  }

  handledDownloadIds.add(downloadItem.id);

  const accepted = await sendToAmid(currentItem, url);
  if (!accepted) {
    handledDownloadIds.delete(downloadItem.id);
    return;
  }

  try {
    await chrome.downloads.cancel(downloadItem.id);
    await chrome.downloads.erase({ id: downloadItem.id });
  } catch (error) {
    console.warn("AMID accepted the download, but Chrome could not cancel its copy.", error);
  }
}

async function getFreshDownloadItem(downloadItem) {
  await delay(250);

  try {
    const matches = await chrome.downloads.search({ id: downloadItem.id });
    return matches[0] || downloadItem;
  } catch {
    return downloadItem;
  }
}

async function sendToAmid(downloadItem, url) {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), AMID_TIMEOUT_MS);

  try {
    const response = await fetch(AMID_ENDPOINT, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        url,
        chromeDownloadId: downloadItem.id,
        filename: downloadItem.filename || "",
        mime: downloadItem.mime || "",
        referrer: downloadItem.referrer || ""
      }),
      signal: controller.signal
    });

    if (!response.ok) {
      return false;
    }

    const result = await response.json();
    return result.accepted === true;
  } catch {
    return false;
  } finally {
    clearTimeout(timeoutId);
  }
}

function isHttpDownload(url) {
  return typeof url === "string" && (url.startsWith("http://") || url.startsWith("https://"));
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}
