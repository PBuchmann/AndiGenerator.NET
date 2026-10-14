// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only
#:package Microsoft.Playwright@1.52.0
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true

// Gegenstück zu pdf.js für Windows ohne Node: druckt anleitung.html im aktuellen Ordner mit Playwright für .NET und dem
// installierten Edge als Anleitung.pdf (gleiche Seitenränder, Fußzeile, Lesezeichen). Aufruf durch anleitung.ps1.
using Microsoft.Playwright;

string ordner = Directory.GetCurrentDirectory();
using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge" });
var seite = await browser.NewPageAsync();
await seite.GotoAsync(new Uri(Path.Combine(ordner, "anleitung.html")).AbsoluteUri, new() { WaitUntil = WaitUntilState.NetworkIdle });
await seite.EvaluateAsync("() => document.fonts.ready");
const string Fuss = "<div style=\"font-family:sans-serif;font-size:7pt;color:#5B6470;width:100%;padding:0 18mm;display:flex;justify-content:space-between\">"
    + "<span>AndiGenerator.NET – Anleitung</span><span>Seite <span class=\"pageNumber\"></span> von <span class=\"totalPages\"></span></span></div>";
await seite.PdfAsync(new()
{
    Path = Path.Combine(ordner, "Anleitung.pdf"),
    Format = "A4",
    PrintBackground = true,
    DisplayHeaderFooter = true,
    HeaderTemplate = "<div></div>",
    FooterTemplate = Fuss,
    PreferCSSPageSize = true,
    Outline = true,
    Tagged = true,
});
Console.WriteLine("Anleitung.pdf geschrieben");
