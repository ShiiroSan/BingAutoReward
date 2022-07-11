// See https://aka.ms/new-console-template for more information
using Microsoft.Playwright;
using System.Net;
using Serilog;
using System.Diagnostics;

/*TODO:
• Replace those with Settings (YAML/JSON/???)
• Add a way to specify the browser (Chromium/Firefox/???)
 */

/**
 * As name is pretty clear actually I'll just explain what happen with different bool.
 
 * • _isInDebug disable the headless mode and set a pause after logging so you can have access to the browser for anything you want. 
 * • _keepData enable the browser to keep the data after we did the job. More data disk cost and stuff but no need for relog at each test.
 *      _keepData is set to true by default.
 *      _keepData ISNT RECOMMENDED. As it basically go in "incognito mode" and it block many things for Quiz for example.
  
 * All of these will be processed differently when Settings will be implemented.
**/
var _isInDebug = false;
var _keepData = true;
/*
 * OFC this part need reworks with encryption and stuff like that. I honestly don't give a care about it. 
 * Implement it, nice. 
 * I'm making it like 1st line is first username, 2nd line 1st passwd, etc.
 * 
 * I'm only doing it that way because I'm lazy and don't want to push it with my username/password in it. 
 */
var ident = File.ReadLines("./id");
var identList = ident.ToList();
var identListPos = 0;
var totalProfile = (identList.Count / 2);


Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate:
        "[{Timestamp:dd/MM/yyyy - HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

Log.Information("Starting!");

/*
 * I'm now using a dictionary of english words. Herokuapp I used before seems to banned me or somewhat. Was expected at some points tbh...
 */
Stopwatch st = new();
st.Start();
var wordsList = File.ReadAllLines("./words");
st.Stop();
Log.Information("Words list loaded in {0}ms.", st.ElapsedMilliseconds);

var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
if (exitCode != 0)
{
    Log.Error($"Playwright exited with code {exitCode}");
    Log.Error($"This is because Playwright wasn't installed I guess. Try restarting the program.");
    throw new Exception($"Playwright exited with code {exitCode}");
}

for (int p = 1; p < totalProfile + 1; p++)
{
    bool AUTOSEARCHBINGDEF = true;
    bool AUTOSEARCHBINGMOBILE = false;
    Log.Information($"Running for profile {p}!");
    using var playwright = await Playwright.CreateAsync();
    IBrowserContext context;
    if (_keepData)
    {
        context = await playwright.Chromium.LaunchPersistentContextAsync(@$"./BingAutoRewards{p}", new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = !_isInDebug,
            SlowMo = 1000,
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.5060.66 Safari/537.36 Edg/103.0.1264.44",
        });
    }
    else
    {
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !_isInDebug,
            SlowMo = 750,
        });
        context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.5060.66 Safari/537.36 Edg/103.0.1264.44",
        });
        await context.NewPageAsync();
    }

    var page = context.Pages[0];
    await page.GotoAsync("https://rewards.microsoft.com/");
    if (page.Url.Contains("welcome"))
    {
        Log.Warning("Need to log.");
        await page.GotoAsync("https://rewards.microsoft.com/Signin");
        await page.FillAsync("[name=loginfmt]", identList[identListPos]);//we get the username
        await page.ClickAsync("[type=submit]");
        await page.FillAsync("[name=passwd]", identList[identListPos + 1]);//we get the password
        await page.ClickAsync("[type=submit]");
        identListPos += 2;
        // TODO: Check for 2FA and handle it. No clue on how to do that.
        if (await page.Locator("#idChkBx_SAOTCAS_TD").CountAsync() != 0)
        {
            await page.ClickAsync("#idChkBx_SAOTCAS_TD");
            try
            {
                await page.WaitForSelectorAsync("[name=DontShowAgain]", new PageWaitForSelectorOptions
                {
                    Timeout = 30000
                });
            }
            catch (Exception ex)
            {
                Log.Error("Cannot log due to exception: {ex}", ex);
            }


        }

        /*!
         * Stay connected all the time. 
         * This part will be modified with Settings incoming. 
         * 
         * Connecting at each time will become part of the features. That allow for no PersistentContext stuff and that's nicer than how I do this actually.
         */
        var dontShowAgainCBoxElem = page.Locator("[name=DontShowAgain]");
        if (await dontShowAgainCBoxElem.CountAsync() != 0)
        {
            await dontShowAgainCBoxElem.ClickAsync();
            await page.ClickAsync("[type=submit]");
        }
    }
    Log.Information("Logged.");
    if (_isInDebug)
    {
        Console.WriteLine("We are in debug. You can browse what you want and need and just press Play when you're done.");
        await page.PauseAsync();
    }
    var cardElem = page.Locator(".rewards-card-container .mee-icon-AddMedium");
    var nbrElem = await cardElem.CountAsync();
    Log.Information("Number of reward card to click: {nbrElem}", nbrElem);
    for (int i = 0; i < nbrElem; i++)
    {
        await cardElem.Nth(i).ClickAsync();

        Log.Debug("{i} clicked", i);
        var cardPage = context.Pages[1];
        await cardPage.WaitForLoadStateAsync(LoadState.Load);
        Log.Debug("Looking for Trivia on Page {i}", i);
        if (await cardPage.Locator(".TriviaOverlayData").CountAsync() > 0) //Used to find if there's Quizz or stuff like that
        {
            Log.Debug("Looking for Poll on Page {i}", i);
            var PollElem = cardPage.Locator("#btPollOverlay");
            var PollNum = await PollElem.CountAsync();
            if (PollNum > 0)
            {
                await PollElem.Locator("#btoption0").ClickAsync();
                Log.Debug("Found poll and clicked first option.");
            }
            else
                Log.Debug("No poll found.");

            Log.Debug("Looking for Quiz on Page {i}", i);
            var QuizWelcomeElem = cardPage.Locator("#quizWelcomeContainer"); //Not started Quiz
            var QuizStartedElem = cardPage.Locator("#currentQuestionContainer"); //Started Quiz
            if (await QuizWelcomeElem.CountAsync() != 0 || await QuizStartedElem.CountAsync() != 0)
            {
                Log.Debug("Found Quiz.");
                /*
                    * Looks like the start quiz button isn't here all the time so we'll just check for it and move on
                    * NVM I'm dumb we'll still check for it because I already programmed it so...
                    */
                if (await cardPage.Locator("#rqStartQuiz").CountAsync() == 1)
                {
                    Log.Debug("Not started Quiz.");
                    await cardPage.ClickAsync("#rqStartQuiz");
                }

                //Read position in quizz and count from already placed position
                var QuizPosHeaderElem = cardPage.Locator("#rqHeaderCredits");
                var QuizPosition = await QuizPosHeaderElem.Locator(".filledCircle").CountAsync();
                for (int quizzPos = QuizPosition; quizzPos < 4; quizzPos++)
                {
                    Log.Debug("Quiz is at pos: {QuizPosition}", QuizPosition);
                    var currentQuestion = cardPage.Locator("#currentQuestionContainer");
                    var correctAnswerElem = currentQuestion.Locator("[iscorrectoption=True]");
                    var CorrectAnswerNum = await correctAnswerElem.CountAsync();
                    for (int j = 0; j < CorrectAnswerNum; j++)
                    {
                        await correctAnswerElem.Nth(j).ClickAsync();
                        Log.Debug("Clicked answer n°{j}/{CorrectAnswerNum}.",
                            j + 1, CorrectAnswerNum);
                    }
                    await cardPage.WaitForLoadStateAsync(LoadState.Load);
                    QuizPosition = await QuizPosHeaderElem.Locator(".filledCircle").CountAsync();
                }
                Log.Debug($"Ended Quiz, yay!");
            }
            else
            {
                Log.Debug("No quizz found.");
            }
        }

        Log.Debug("Closing Page {i}...", i);
        await cardPage.CloseAsync();
    }


    /* TODO: Add a bit of randomization to make it more human
     */
    var profileLevel = await page.Locator(".level").AllInnerTextsAsync();
    var profileLevelNumeric = int.Parse(System.Text.RegularExpressions.Regex.Match(profileLevel[0], @"\d+").Value);
    int maxDesktopSearch = 30, maxMobileSearch = 20, initialDeskopSearchPos = 0, initialMobileSearchPos = 0;

    const int constPointsPerSearch = 3;

    Log.Information("Profile {p} is level {profileLevelNumeric}.", p, profileLevelNumeric);
    var rawPointsCounter = page.Locator(".pointsDetail > .ng-binding");

    var rawPointsComputerCounterText = await rawPointsCounter.Nth(0).InnerTextAsync();
    var regexPointCounterComputer = System.Text.RegularExpressions.Regex.Matches(rawPointsComputerCounterText, @"([0-9]+)\s\/\s([0-9]+)");
    initialDeskopSearchPos = int.Parse(regexPointCounterComputer[0].Groups[1].Value) / constPointsPerSearch;
    maxDesktopSearch = int.Parse(regexPointCounterComputer[0].Groups[2].Value) / constPointsPerSearch;
    //show debug max and initial search position
    Log.Debug("Desktop search option: {initialDeskopSearchPos}/{maxDesktopSearch}.", initialDeskopSearchPos, maxDesktopSearch);

    if (profileLevelNumeric == 2)
    {
        var rawPointsMobileCounterText = await rawPointsCounter.Nth(1).InnerTextAsync();
        var regexPointCounterMobile = System.Text.RegularExpressions.Regex.Matches(rawPointsMobileCounterText, @"([0-9]+)\s\/\s([0-9]+)");
        initialMobileSearchPos = int.Parse(regexPointCounterMobile[0].Groups[1].Value) / constPointsPerSearch;
        maxMobileSearch = int.Parse(regexPointCounterMobile[0].Groups[2].Value) / constPointsPerSearch;
        //show debug max and initial search for mobile
        Log.Debug("Mobile search option: {initialMobileSearchPos}/{maxMobileSearch}.", initialMobileSearchPos, maxMobileSearch);

        AUTOSEARCHBINGMOBILE = true;
        Log.Information("We'll do mobile search for profile {p}.", p);

    }
    Random rnd = new();
    var client = new HttpClient();
    if (AUTOSEARCHBINGDEF)
    {
        Log.Information("Starting autosearch desktop.");
        var searchPage = await context.NewPageAsync();
        for (int i = initialDeskopSearchPos; i < maxDesktopSearch; i++)
        {
            string randomWord = wordsList[rnd.Next(wordsList.Length)];
            var searchURL = $"https://www.bing.com/search?q={randomWord}";
            Log.Debug("Search n°{i}/30      {searchURL}", i + 1, searchURL);
            await searchPage.GotoAsync(searchURL);
            await searchPage.WaitForLoadStateAsync(LoadState.Load);
        }
        Log.Information("Ended autosearch desktop.");
    }
    Log.Information("Closing Context as it's not needed anymore");
    await context.CloseAsync();

    if (AUTOSEARCHBINGMOBILE)
    {
        Log.Information("Starting autosearch for mobile.");
        var contextMobile = await playwright.Chromium.LaunchPersistentContextAsync(@$".\BingAutoRewards{p}", new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = !_isInDebug,
            SlowMo = 750,
            UserAgent = "Mozilla/5.0 (Linux; Android 12; Pixel 6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.104 Mobile Safari/537.36",
        });
        Log.Information("Initiated mobile context.");
        for (int i = initialMobileSearchPos; i < maxMobileSearch; i++)
        {
            string randomWord = wordsList[rnd.Next(wordsList.Length)];
            var searchURL = $"https://www.bing.com/search?q={randomWord}";
            Log.Debug("Search n°{i}/20      {searchURL}", i + 1, searchURL);
            await contextMobile.Pages[0].GotoAsync(searchURL);
            await contextMobile.Pages[0].WaitForLoadStateAsync(LoadState.Load);
        }
        Log.Information("Ended autosearch mobile.");
        Log.Information("Closing Mobile Context as it's not needed anymore");
        await contextMobile.CloseAsync();
    }
    Log.Information($"Done for profile {p}");
    playwright.Dispose();
}
Log.Information($"Done!");