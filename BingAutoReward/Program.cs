// See https://aka.ms/new-console-template for more information
using Microsoft.Playwright;
using Serilog;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;

/*TODO:
• Replace those with Settings (YAML/JSON/???)
• Add a way to specify the browser (Chromium/Firefox/???)
 */

/**
 * As name is pretty clear actually I'll just explain what happen with different parameter.
 
 * • _isInDebug disable the headless mode and set a pause after logging so you can have access to the browser for anything you want. 
 * • _keepData enable the browser to keep the data after we did the job. More data disk cost and stuff but no need for relog at each test.
 *      _keepData is set to true by default.
 *      _keepData ISNT RECOMMENDED. As it basically go in "incognito mode" and it block many things for Quiz for example.
 * • _maxRetry is the number of retry if some searchs failed. If this value is -1, it will try to search until we got the points, if 0 it will stop after the first try.
 *      _maxRetry is set to 5 by default.
 *      -1 isn't recommended as it will try to search until we got the points, it could do infinite searchs. (And it will potentially spam bing and increase detection IDK, IDC)
 * • _redeemInfo get info on the redeem you want to do and display the completion on the console and log. 
 *      _redeemInfo is set to true by default.
 *      nothing will be redeemed if we are at 100% completion. That's your only task ffs...
  
 * All of these will be processed differently when Settings will be implemented.
**/

//TODO: Add option for Telegram Bot/Discord Webhook and every other possible output.
bool _isInDebug = true;
bool _keepData = true;
int _maxRetry = 5;
bool _redeemInfo = true;

#if RELEASE
_isInDebug = false;
#endif

Log.Information("Starting!");
/*
 * OFC this part need reworks with encryption and stuff like that. I honestly don't give a care about it. 
 * Implement it, nice. 
 * I'm making it like 1st line is first username, 2nd line 1st passwd, etc.
 * 
 * I'm only doing it that way because I'm lazy and don't want to push it with my username/password in it. 
 */
IEnumerable<string> ident = File.ReadLines("./id");
List<string> identList = ident.ToList();
int identListPos = 0;
int totalProfile = (identList.Count / 2);

Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate:
        "[{Timestamp:dd/MM/yyyy - HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

/*
 * Add your telegram bot API in telegramAPI file or uuuh just replace it in the code. 
 * First line is the bot token, second line is the chat id.
 * Just like above, I'm lazy :)
 */
var telegramBotToken = File.ReadLines("./telegramAPI");
var telegramBot = new TelegramBotClient(telegramBotToken.ToList()[0]);
var _chatId = telegramBotToken.ToList()[1];
var me = await telegramBot.GetMeAsync();

AppDomain currentDomain = default;
currentDomain = AppDomain.CurrentDomain;
// Handler for unhandled exceptions.
currentDomain.UnhandledException += GlobalUnhandledExceptionHandler;


/*
 * I'm now using a dictionary of english words. Herokuapp I used before seems to banned me or somewhat. Was expected at some points tbh...
 */
Stopwatch st = new();
st.Start();
var wordsList = File.ReadAllLines("./words");
st.Stop();
Log.Information("Words list loaded in {0}ms.", st.ElapsedMilliseconds);

int exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
if (exitCode != 0)
{
    Log.Error($"Playwright exited with code {exitCode}");
    Log.Error($"This is because Playwright wasn't installed I guess. Try restarting the program.");
    throw new Exception($"Playwright exited with code {exitCode}");
}

/*
 * This is the main loop.
 * It will loop through all the profiles and do the job.
 * 
 * I'm not sure if it's the best way to do it but it works.
 */

//TODO: Add a way to know if a profile have Game Pass subscription and automatically "run" a game each day.
//TODO: Port it to Linux so I can be ran on a VPS 😁 <- Mono doesn't work with .NET6, need to do a backward things.

for (int p = 1; p < totalProfile + 1; p++)
{
    List<string> rawListString = new();
    bool autoSearchDesktop = true;
    bool autoSearchMobile = false;
    Log.Information($"Running for profile {p}!");
    using IPlaywright playwright = await Playwright.CreateAsync();
    IBrowserContext context;
    //TODO: Handle CultureInfo
    if (_keepData)
    {
        context = await playwright.Chromium.LaunchPersistentContextAsync(@$"./BingAutoRewards{p}", new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = !_isInDebug,
            SlowMo = 1000,
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.5060.66 Safari/537.36 Edg/103.0.1264.44",
            //Locale = "en-GB"
        });
    }
    else
    {
        IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !_isInDebug,
            SlowMo = 1000,
        });
        context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.5060.66 Safari/537.36 Edg/103.0.1264.44",
        });
        await context.NewPageAsync();
    }

    IPage page = context.Pages[0];
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
        ILocator dontShowAgainCBoxElem = page.Locator("[name=DontShowAgain]");
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
    ILocator profileNameElem = page.Locator(".l_header_right>a.additional_info");
    string profileName = await profileNameElem.InnerTextAsync();
    Log.Debug("Profile name: {profileName}", profileName);
    await telegramBot.SendTextMessageAsync(
    chatId: _chatId,
    text: $"Starting for {profileName}.");
    var cultureInfo = page.Locator(".c-uhff-lang-selector");

    ILocator initialPointsElem_New = page.Locator("mee-rewards-user-status-banner-balance .pointsValue mee-rewards-counter-animation span");
    ILocator initialPointsElem_Old = page.Locator("mee-rewards-user-status-balance .number mee-rewards-counter-animation span");
    ILocator initialPointsElem;
    if (await initialPointsElem_New.CountAsync() != 0)
        initialPointsElem = initialPointsElem_New;
    else
        initialPointsElem = initialPointsElem_Old;

    Log.Debug("initalPoint hex: {hex}", Convert.ToHexString(Encoding.UTF8.GetBytes(await initialPointsElem.InnerTextAsync())));
    Log.Debug("initalPoint str: {hex}", await initialPointsElem.InnerTextAsync());
    var test = (await initialPointsElem.InnerTextAsync()).Replace("\u202F", "");
    //TODO: Handle CultureInfo in a better way. We won't force people to use a language they don't talk y'a know
    if (test.Contains(","))
    {
        test = test.Replace(",", "");
    }
    int initialPoints = int.Parse(test);
    int newerPoints = 0;
    for (int cardTries = 0; cardTries < _maxRetry; cardTries++)
    {
        if (_maxRetry != 0 && cardTries != 0)
        {
            Log.Information("Card tries: {cardTries}", cardTries);
            await page.ReloadAsync();
        }
        ILocator cardElem = page.Locator(".rewards-card-container .mee-icon-AddMedium");
        int nbrElem = await cardElem.CountAsync();
        Log.Information("Number of reward card to click: {nbrElem}", nbrElem);
        if (nbrElem == 0)
        {
            break;
        }
        for (int i = 0; i < nbrElem; i++)
        {
            await cardElem.Nth(i).ClickAsync();
            Thread.Sleep(400);

            Log.Debug("{i} clicked", i);
            IPage cardPage = context.Pages[1];
            await cardPage.WaitForLoadStateAsync(LoadState.Load);
            Log.Debug("Looking for Trivia on Page {i}", i);
            if (await cardPage.Locator(".TriviaOverlayData").CountAsync() > 0) //Used to find if there's Quizz or stuff like that
            {
                Log.Debug("Looking for Poll on Page {i}", i);
                ILocator PollElem = cardPage.Locator("#btPollOverlay");
                int PollNum = await PollElem.CountAsync();
                if (PollNum > 0)
                {
                    await PollElem.Locator("#btoption0").ClickAsync();
                    Log.Debug("Found poll and clicked first option.");
                }
                else
                    Log.Debug("No poll found.");

                Log.Debug("Looking for Quiz on Page {i}", i);
                //ILocator QuizWelcomeElem = cardPage.Locator("#quizWelcomeContainer"); //Not started Quiz
                ILocator QuizStartedElem = cardPage.Locator("#currentQuestionContainer"); //Started Quiz
                if (/*await QuizWelcomeElem.CountAsync() != 0 || */await QuizStartedElem.CountAsync() != 0)
                {
                    Log.Debug("Found Quiz.");
                    if (await cardPage.Locator("#rqStartQuiz").CountAsync() == 1)
                    {
                        Log.Debug("Not started Quiz.");
                        await cardPage.ClickAsync("#rqStartQuiz");
                    }
                    //based on https://github.com/charlesbel/Microsoft-Rewards-Farmer/blob/30c26d30ef0730183fe8bbda6ba24c1371b05e33/ms_rewards_farmer.py#L643
                    if (await cardPage.Locator(".bt_optionVS").CountAsync() != 0)
                    {
                        Log.Debug("Found This or That Quiz.");
                        var progressElem = cardPage.Locator(".bt_Quefooter");
                        var progressText = await progressElem.InnerTextAsync();
                        var match = Regex.Matches(progressText, @"\d+");
                        var maxPos = int.Parse(match[1].Value);
                        var actualPos = int.Parse(match[0].Value);
                        for (int j = actualPos; j < maxPos + 1; j++)
                        {
                            Log.Debug("This or That Position : {j}/{maxPos}", j, maxPos);
                            var possibleAnswer1 = cardPage.Locator("#rqAnswerOption0");
                            var possibleAnswer2 = cardPage.Locator("#rqAnswerOption1");

                            var encodedKey = await cardPage.EvaluateAsync<string>("_G.IG");
                            Log.Debug("Encoded Answer: {encodedKey}", encodedKey);
                            var answerCode = await cardPage.EvaluateAsync<string>("_w.rewardsQuizRenderInfo.correctAnswer");
                            Log.Debug("Answer Code: {answerCode}", answerCode);

                            var title1 = await possibleAnswer1.GetAttributeAsync("data-option");
                            Log.Debug("Answer title 1: {title1}", title1);
                            string decodedAnswer1 = "";
                            if (title1 != null)
                            {
                                decodedAnswer1 = decodeAnswerBasedOnKey(encodedKey, title1);
                                Log.Debug("Decoded Answer: {decodedAnswer1}", decodedAnswer1);
                            }
                            var title2 = await possibleAnswer2.GetAttributeAsync("data-option");
                            Log.Debug("Answer title 2: {title2}", title2);
                            string decodedAnswer2 = "";
                            if (title2 != null)
                            {
                                decodedAnswer2 = decodeAnswerBasedOnKey(encodedKey, title2);
                                Log.Debug("Decoded Answer: {decodedAnswer2}", decodedAnswer2);
                            }
                            //if decodedAnswer1 is equal to answerCode, click on possibleAnswer1 else if decodedAnswer2 is equal to answerCode click on possibleAnswer2
                            if (decodedAnswer1 == answerCode)
                            {
                                Log.Debug("Click on {title1}.", title1);
                                await possibleAnswer1.ClickAsync();
                            }
                            else if (decodedAnswer2 == answerCode)
                            {
                                Log.Debug("Click on {title2}.", title2);
                                await possibleAnswer2.ClickAsync();
                            }
                            else
                            {
                                Log.Error("Answer not found.");
                            }

                            await cardPage.WaitForLoadStateAsync(LoadState.Load);
                        }
                    }
                    else
                    {
                        Log.Debug("Found Normal Quiz.");
                        //Read position in quizz and count from already placed position
                        ILocator QuizPosHeaderElem = cardPage.Locator("#rqHeaderCredits");
                        int QuizPosition = await QuizPosHeaderElem.Locator(".filledCircle").CountAsync();

                        //TODO: Rework this part. It's not working as it should.
                        ILocator multiChoiceElem = cardPage.Locator(".textBasedMultiChoice");
                        if (await multiChoiceElem.CountAsync() != 0) //we are in multi choice quizz and that sucks
                        {
                            for (int quizzPos = QuizPosition; quizzPos < 4; quizzPos++)
                            {
                                var correctAnswer = await cardPage.EvaluateAsync<string>("_w.rewardsQuizRenderInfo.correctAnswer");
                                Log.Debug("Quiz is at pos: {QuizPosition}", quizzPos);
                                ILocator currentQuestion = cardPage.Locator("#currentQuestionContainer");
                                ILocator answerElem = currentQuestion.Locator(".rq_button .rqOption");
                                int answerNum = await answerElem.CountAsync();
                                for (int j = 0; j < answerNum; j++)
                                {
                                    var buttonText = await answerElem.Nth(j).GetAttributeAsync("data-option");
                                    if (buttonText == correctAnswer)
                                    {
                                        await answerElem.Nth(j).ClickAsync();
                                        break;
                                    }

                                }
                                await cardPage.WaitForLoadStateAsync(LoadState.Load);
                            }
                        }
                        else //normal quiz nice
                        {
                            for (int quizzPos = QuizPosition; quizzPos < 4; quizzPos++)
                            {
                                Log.Debug("Quiz is at pos: {QuizPosition}", QuizPosition);
                                ILocator currentQuestion = cardPage.Locator("#currentQuestionContainer");
                                ILocator correctAnswerElem = currentQuestion.Locator("[iscorrectoption=True]");
                                int CorrectAnswerNum = await correctAnswerElem.CountAsync();
                                for (int j = 0; j < CorrectAnswerNum; j++)
                                {
                                    await correctAnswerElem.Nth(j).ClickAsync();
                                    Log.Debug("Clicked answer n°{j}/{CorrectAnswerNum}.",
                                        j + 1, CorrectAnswerNum);
                                }
                                await cardPage.WaitForLoadStateAsync(LoadState.Load);
                                QuizPosition = await QuizPosHeaderElem.Locator(".filledCircle").CountAsync();
                            }
                        }
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
    }

    /* TODO: 
     * • Add a bit of randomization to make it more human
     */
    var profileLevel_New = page.Locator(".persona .profileDescription");
    var profileLevel_Old = page.Locator(".level");
    IReadOnlyList<string> profileLevel;
    if (await profileLevel_New.CountAsync() != 0)
        profileLevel = await profileLevel_New.AllInnerTextsAsync();
    else
        profileLevel = await profileLevel_Old.AllInnerTextsAsync();

    int profileLevelNumeric = int.Parse(Regex.Match(profileLevel[0], @"\d+").Value);
    int maxDesktopSearch = 30, maxMobileSearch = 20, initialDeskopSearchPos = 0, initialMobileSearchPos = 0;

    const int constPointsPerSearch = 3;

    Log.Information("Profile {p} is level {profileLevelNumeric}.", p, profileLevelNumeric);
    await page.GotoAsync("https://rewards.microsoft.com/pointsbreakdown");
    ILocator rawPointsCounter = page.Locator(".pointsDetail.ng-binding");
    if (profileLevelNumeric == 2)
    {
        autoSearchMobile = true;
        Log.Information("We'll do mobile search for profile {p}.", p);
    }
    Random rnd = new();
    var counterPos = 0;
    if (autoSearchDesktop)
    {
        int searchTry = 0;
        await page.GotoAsync("https://rewards.microsoft.com/pointsbreakdown");

        if (await rawPointsCounter.Nth(0).GetAttributeAsync("ng-if") == "!$ctrl.pointsSummary.isLevelTaskComplete")
            counterPos++;

        String rawPointsComputerCounterText = await rawPointsCounter.Nth(counterPos + 1).InnerTextAsync();

        MatchCollection regexPointCounterComputer = Regex.Matches(rawPointsComputerCounterText, @"([0-9]+)\s\/\s([0-9]+)");
        initialDeskopSearchPos = int.Parse(regexPointCounterComputer[0].Groups[1].Value) / constPointsPerSearch;
        maxDesktopSearch = int.Parse(regexPointCounterComputer[0].Groups[2].Value) / constPointsPerSearch;
        Log.Debug("Desktop search option: {initialDeskopSearchPos}/{maxDesktopSearch}.", initialDeskopSearchPos, maxDesktopSearch);
        if (_maxRetry == -1)
        {
            _maxRetry = int.MaxValue;
        }
        while (searchTry < _maxRetry && initialDeskopSearchPos != maxDesktopSearch)
        {
            if (regexPointCounterComputer[0].Groups[1].Value != regexPointCounterComputer[0].Groups[2].Value)
            {
                rawPointsComputerCounterText = await rawPointsCounter.Nth(counterPos + 1).InnerTextAsync();
                regexPointCounterComputer = Regex.Matches(rawPointsComputerCounterText, @"([0-9]+)\s\/\s([0-9]+)");
                if (searchTry != 0)
                    Log.Error("Some search didn't count!");

                initialDeskopSearchPos = int.Parse(regexPointCounterComputer[0].Groups[1].Value) / constPointsPerSearch;
                maxDesktopSearch = int.Parse(regexPointCounterComputer[0].Groups[2].Value) / constPointsPerSearch;
                //show debug max and initial search position
                Log.Debug("Desktop search option: {initialDeskopSearchPos}/{maxDesktopSearch}.", initialDeskopSearchPos, maxDesktopSearch);
                Log.Debug("Number of search to do: {nbrToDo}", maxDesktopSearch - initialDeskopSearchPos);
                Log.Information("Starting autosearch for desktop n°{searchTry}/{_maxRetry}.", searchTry + 1, _maxRetry);
                var searchPage = await context.NewPageAsync();
                for (int i = initialDeskopSearchPos; i < maxDesktopSearch; i++)
                {
                    string randomWord = wordsList[rnd.Next(wordsList.Length)];
                    string searchURL = $"https://www.bing.com/search?q={randomWord}";
                    Log.Debug("Search n°{i}/{maxDesktopSearch}      {searchURL}", i + 1, maxDesktopSearch, searchURL);
                    await searchPage.GotoAsync(searchURL);
                    await searchPage.WaitForLoadStateAsync(LoadState.Load);
                }
                Log.Information("Ended autosearch n°{searchTry} desktop.", searchTry++);
                searchTry++;
            }
            if (_maxRetry == int.MaxValue)
            {
                searchTry = 0;
            }
            await page.ReloadAsync();
            await page.WaitForLoadStateAsync(LoadState.Load);
        }
    }
    Log.Information("Ended autosearch desktop.");

    //TODO: Add retry search if not at max points

    if (autoSearchMobile)
    {
        int searchTry = 0;

        String rawPointsMobileCounterText = await rawPointsCounter.Nth(counterPos).InnerTextAsync();
        System.Text.RegularExpressions.MatchCollection regexPointCounterMobile = System.Text.RegularExpressions.Regex.Matches(rawPointsMobileCounterText, @"([0-9]+)\s\/\s([0-9]+)");
        initialMobileSearchPos = int.Parse(regexPointCounterMobile[0].Groups[1].Value) / constPointsPerSearch;
        maxMobileSearch = int.Parse(regexPointCounterMobile[0].Groups[2].Value) / constPointsPerSearch;
        //show debug max and initial search for mobile
        Log.Debug("Mobile search option: {initialMobileSearchPos}/{maxMobileSearch}.", initialMobileSearchPos, maxMobileSearch);
        if (initialMobileSearchPos != maxMobileSearch)
        {
            await context.CloseAsync();
            Log.Information("Closing Context as it's not needed anymore");
            Log.Information("Starting autosearch for mobile.");
            //TODO: Add no keep data option even if it sucks to do this.
            IBrowserContext contextMobile = await playwright.Chromium.LaunchPersistentContextAsync(@$".\BingAutoRewards{p}", new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = !_isInDebug,
                SlowMo = 1000,
                UserAgent = "Mozilla/5.0 (Linux; Android 12; Pixel 3 XL) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.101 Mobile Safari/537.36",
            });
            Log.Information("Initiated mobile context.");
            if (_maxRetry == -1)
                _maxRetry = int.MaxValue;
            IPage pageMobile = contextMobile.Pages[0];
            while (searchTry < _maxRetry && initialMobileSearchPos != maxMobileSearch)
            {

                if (initialMobileSearchPos != maxMobileSearch)
                {
                    await pageMobile.GotoAsync("https://rewards.microsoft.com/pointsbreakdown");
                    await pageMobile.WaitForLoadStateAsync(LoadState.Load);
                    ILocator rawPointsCounterMobile = pageMobile.Locator(".pointsDetail.ng-binding");

                    rawPointsMobileCounterText = await rawPointsCounterMobile.Nth(counterPos).InnerTextAsync();
                    regexPointCounterMobile = Regex.Matches(rawPointsMobileCounterText, @"([0-9]+)\s\/\s([0-9]+)");
                    initialMobileSearchPos = int.Parse(regexPointCounterMobile[0].Groups[1].Value) / constPointsPerSearch;
                    maxMobileSearch = int.Parse(regexPointCounterMobile[0].Groups[2].Value) / constPointsPerSearch;
                    Log.Debug("Mobile search option: {initialMobileSearchPos}/{maxMobileSearch}.", initialMobileSearchPos, maxMobileSearch);
                    Log.Debug("Number of search to do: {nbrToDo}", maxMobileSearch - initialMobileSearchPos);
                    Log.Information("Starting autosearch for desktop n°{searchTry}/{_maxRetry}.", searchTry + 1, _maxRetry);
                    await contextMobile.NewPageAsync();
                    for (int i = initialMobileSearchPos; i < maxMobileSearch; i++)
                    {
                        string randomWord = wordsList[rnd.Next(wordsList.Length)];
                        string searchURL = $"https://www.bing.com/search?q={randomWord}";
                        Log.Debug("Search n°{i}/{maxMobileSearch}      {searchURL}", i + 1, maxMobileSearch, searchURL);
                        await contextMobile.Pages[1].GotoAsync(searchURL);
                        await contextMobile.Pages[1].WaitForLoadStateAsync(LoadState.Load);
                    }
                    Log.Information("Ended autosearch mobile.");
                }
                else
                    Log.Information("No search to do for mobile.");
                searchTry++;
                if (_maxRetry == int.MaxValue)
                    searchTry = 0;

                await pageMobile.ReloadAsync();
                await pageMobile.WaitForLoadStateAsync(LoadState.Load);
            }
            Log.Information("Closing Mobile Context as it's not needed anymore");
            await contextMobile.CloseAsync();
        }
    }
    if (context.Pages.Count == 0)
    {
        //TODO: Add no keep data option even if it sucks to do so.
        if (_keepData)
        {
            context = await playwright.Chromium.LaunchPersistentContextAsync(@$"./BingAutoRewards{p}", new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = !_isInDebug,
                SlowMo = 1000,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.5060.66 Safari/537.36 Edg/103.0.1264.44",
            });
            page = context.Pages[0];
            await page.GotoAsync("https://rewards.microsoft.com/");
        }
    }
    if (_redeemInfo)
    {
        ILocator redeemInfoCard = page.Locator("mee-rewards-redeem-info-card");
        ILocator setGoalElem = redeemInfoCard.Locator("#dashboard-set-goal");
        ILocator progressGoal = setGoalElem.Locator(".c-progress");
        ILocator PointsElem_New = page.Locator("mee-rewards-user-status-banner-balance .pointsValue mee-rewards-counter-animation span");
        ILocator PointsElem_Old = page.Locator("mee-rewards-user-status-balance .number mee-rewards-counter-animation span");
        ILocator pointsElem;
        if (await PointsElem_New.CountAsync() != 0)
            pointsElem = PointsElem_New;
        else
            pointsElem = PointsElem_Old;
        newerPoints = int.Parse((await pointsElem.InnerTextAsync()).Replace("\u202F", ""));
        Log.Information("Current points: {newerPoints} ({diffPoints})", newerPoints, newerPoints - initialPoints);
        if (await progressGoal.CountAsync() != 0)
        {
#pragma warning disable CS8600 // Conversion de littéral ayant une valeur null ou d'une éventuelle valeur null en type non-nullable.
            string goalPercentage = await progressGoal.GetAttributeAsync("value");
#pragma warning restore CS8600 // Conversion de littéral ayant une valeur null ou d'une éventuelle valeur null en type non-nullable.
            ILocator textGoalElem = setGoalElem.Locator("[mee-heading=subheading4]");
            string goalText = await textGoalElem.InnerTextAsync();
            string graphicalPercentage = "[";
            if (goalPercentage != null)
            {
                int numOfSquare = (int)Math.Round(double.Parse(goalPercentage, new System.Globalization.CultureInfo("en-US")) / 10);
                graphicalPercentage += new string('█', numOfSquare);
                graphicalPercentage += new string('░', 10 - numOfSquare);
                graphicalPercentage += "]";
                rawListString.Add($"{graphicalPercentage}  {Math.Truncate(double.Parse(goalPercentage, new System.Globalization.CultureInfo("en-US")))}%\n{goalText}");
                Log.Information("{graphicalPercentage}  {percentage}%\n{goalText}", graphicalPercentage, Math.Truncate(double.Parse(goalPercentage, new System.Globalization.CultureInfo("en-US"))), goalText); ;
                if (numOfSquare == 10)
                {
                    rawListString.Add("Goal reached!");
                    Log.Information("Goal reached!");
                }
            }   //show goal
        }
        else
        {
            Log.Information("No goal set.");
        }
    }
    string botMessageString = $"Done for {profileName}\n" +
        "Current points: " + newerPoints + " (+" + (newerPoints - initialPoints) + ")\n" +
        rawListString[0];
    await telegramBot.SendTextMessageAsync(
    chatId: _chatId,
    text: botMessageString);
    Log.Information($"Sent Telegram info.");
    Log.Information("Done for {profileName}     {p}/{totalProfile}", profileName, p, totalProfile);
    playwright.Dispose();
}
Log.Information($"Done!");
Console.ReadKey();


//based on https://github.com/charlesbel/Microsoft-Rewards-Farmer/blob/30c26d30ef0730183fe8bbda6ba24c1371b05e33/ms_rewards_farmer.py#L262
static string decodeAnswerBasedOnKey(string key, string answer)
{
    int decodedAnswer = 0;
    for (int i = 0; i < answer.Length; i++)
    {
        decodedAnswer += (int)(answer[i]);
    }
    decodedAnswer += int.Parse(key[^2..], System.Globalization.NumberStyles.HexNumber);
    return decodedAnswer.ToString();
}

void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
{
    Exception ex = default;
    ex = (Exception)e.ExceptionObject;
    Log.Error(ex.Message + "\n" + ex.StackTrace);
    //send log to telegram
    telegramBot.SendTextMessageAsync(
    chatId: _chatId,
    text: ex.Message + "\n" + ex.StackTrace);

}