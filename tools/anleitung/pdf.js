const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto('file://' + process.cwd() + '/anleitung.html', { waitUntil: 'networkidle' });
  await page.evaluate(() => document.fonts.ready);
  const fuss = '<div style="font-family:sans-serif;font-size:7pt;color:#5B6470;width:100%;padding:0 18mm;display:flex;justify-content:space-between">'
    + '<span>AndiGenerator.NET – Anleitung</span><span>Seite <span class="pageNumber"></span> von <span class="totalPages"></span></span></div>';
  await page.pdf({ path: 'Anleitung.pdf', format: 'A4', printBackground: true, displayHeaderFooter: true,
    headerTemplate: '<div></div>', footerTemplate: fuss, preferCSSPageSize: true, outline: true, tagged: true });
  await browser.close();
})();
