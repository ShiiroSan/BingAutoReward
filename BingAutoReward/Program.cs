// See https://aka.ms/new-console-template for more information
using Microsoft.Playwright;
using System.Net;
using Serilog;

/*
 * OFC this part need reworks with encryption and stuff like that. I honestly don't give a care about it. 
 * Implement it, nice. 
 * I'm making it like 1st line is first username, 2nd line 1st passwd, etc.
 * 
 * I'm only doing it that way because I'm lazy and don't want to push it with my username/password in it. 
 */
var ident = File.ReadLines("./id");

string emailProfile1 = ident.ToList()[0];
string passwdProfile1 = ident.ToList()[1];

string emailProfile2 = ident.ToList()[2];
string passwdProfile2 = ident.ToList()[3];

bool AUTOSEARCHBINGDEF = true;
bool AUTOSEARCHBINGMOBILE = true;


Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate:
        "[{Timestamp:dd/MM/yyyy - HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

Log.Information("Starting!");

var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
if (exitCode != 0)
{
    Log.Error($"Playwright exited with code {exitCode}");
    throw new Exception($"Playwright exited with code {exitCode}");
}

for (int p = 1; p < 3; p++)
{
    Log.Information($"Running for profile {p}!");
    using var playwright = await Playwright.CreateAsync();
    var context = await playwright.Chromium.LaunchPersistentContextAsync(@$"./BingAutoRewards{p}", new BrowserTypeLaunchPersistentContextOptions
    {
        Headless = true,
        SlowMo = 500,
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.5060.66 Safari/537.36 Edg/103.0.1264.44",
    });
    var page = context.Pages[0];
    await page.GotoAsync("https://rewards.microsoft.com/");
    if (page.Url.Contains("welcome"))
    {
        Log.Warning("Need to log.");
        await page.GotoAsync("https://rewards.microsoft.com/Signin");
        if (p==1)
        {
            await page.FillAsync("[name=loginfmt]", emailProfile1);
        }
        else
        {
            await page.FillAsync("[name=loginfmt]", emailProfile2);
        }
        await page.ClickAsync("[type=submit]");
        if (p==1)
        {
            await page.FillAsync("[name=passwd]", passwdProfile1);
        }
        else
            await page.FillAsync("[name=passwd]", passwdProfile2);
        await page.ClickAsync("[type=submit]");
        if (p == 1)
        {
            await page.ClickAsync("#idChkBx_SAOTCAS_TD");
        }
    }
    else
    {
        Log.Information("Already logged.");
        var cardElem = page.Locator(".rewards-card-container .mee-icon-AddMedium");
        var nbrElem = await cardElem.CountAsync();
        Log.Information("Number of reward card to click: {nbrElem}", nbrElem);
        for (int i = 0; i < nbrElem; i++)
        {
            await cardElem.Nth(i).ClickAsync();
            Log.Debug("{i} clicked", i);
        }
        var allPages = context.Pages;
        /*
         * Starting in dec so we can close pages one after the other. 
         * Stoping under 1 because we want to ignore page 0 (main reward page)
         */
        Log.Information($"Number of Pages open: {context.Pages.Count}");
        Log.Information("Looking for poll on all pages...");
        for (int i = allPages.Count - 1; i > 0; i--)
        {
            Log.Debug("Looking for Poll on Page {i}", i);
            var PollElem = allPages[i].Locator("#btPollOverlay");
            var nbrPoll = await PollElem.CountAsync();
            if (nbrPoll > 0)
            {
                await PollElem.Locator("#btoption0").ClickAsync();
                Log.Debug("Found poll and clicked first option.");
            }
            Log.Debug("Closing Page {i}...", i);
            await allPages[i].CloseAsync();
        }

        var client = new HttpClient();

        if (AUTOSEARCHBINGDEF)
        {
            Log.Information("Starting autosearch desktop.");
            var searchPage = await context.NewPageAsync();
            for (int i = 0; i < 30; i++)
            {
                var randomWord = await client.GetStringAsync("https://random-word-api.herokuapp.com/word");
                randomWord = randomWord.Trim('[').Trim(']').Trim('"');
                var searchURL = $"https://www.bing.com/search?q={randomWord}";
                Log.Debug("Search n°{i}/30      {searchURL}", i + 1, searchURL);
                await searchPage.GotoAsync(searchURL);
            }
            Log.Information("Ended autosearch desktop.");
        }
        Log.Information("Closing Context as it's not needed anymore");
        await context.CloseAsync();

        if (AUTOSEARCHBINGMOBILE)
        {
            Log.Information("Starting autosearch for mobile.");
            var contextMobile = await playwright.Chromium.LaunchPersistentContextAsync(@"C:\BingAutoRewards", new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = true,
                SlowMo = 500,
                UserAgent = "Mozilla/5.0 (Linux; Android 12; Pixel 6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.104 Mobile Safari/537.36",
            });
            Log.Information("Initiated mobile context.");
            for (int i = 0; i < 20; i++)
            {
                var randomWord = await client.GetStringAsync("https://random-word-api.herokuapp.com/word");
                randomWord = randomWord.Trim('[').Trim(']').Trim('"');
                var searchURL = $"https://www.bing.com/search?q={randomWord}";
                Log.Debug("Search n°{i}/20      {searchURL}", i + 1, searchURL);
                await contextMobile.Pages[0].GotoAsync(searchURL);
            }
            Log.Information("Ended autosearch mobile.");
            Log.Information("Closing Mobile Context as it's not needed anymore");
            await contextMobile.CloseAsync();
        }
        Log.Information($"Done for profile {p}");
    }
    playwright.Dispose();
}
Log.Information($"Done!");