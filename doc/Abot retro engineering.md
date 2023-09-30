# Abot 2.0.70

## Main execution flow

### 0. Initialize web crawler

public WebCrawler()
- CrawlConfiguration crawlConfiguration : *Configurable crawl values*
- ICrawlDecisionMaker crawlDecisionMaker : *Decides whether or not to crawl a page or that page's links*
- IThreadManager threadManager : *Distributes http requests over multiple threads*
- IScheduler scheduler : *Decides what link should be crawled next*
- IPageRequester pageRequester : *Makes the raw http requests*
- IHtmlParser htmlParser : *Parses a crawled page for it's hyperlinks*
- IMemoryManager memoryManager : *Checks the memory usage of the host process*

### 1. Start with root Uri

*WebCrawler.CrawlAsync(Uri, CancellationTokenSource)*

- CrawlContext.RootUri = uri

- rootPage = new PageToCrawl( uri )

- if **ShouldSchedulePageLink**(rootPage)

- **scheduler.Add**( rootPage )

### 2. Main loop

*WebCrawler.CrawlSite()*

while (!crawlComplete)
  
- CheckForCancellationRequest() 
  - if crawlContext.CancellationTokenSource.IsCancellationRequested
  - rawlResult.ErrorException = new OperationCanceledException()
  - crawlContext.IsCrawlHardStopRequested = true
- CheckForHardStopRequest() 
  - if crawlContext.IsCrawlHardStopRequested
  - after an unknown exception occured in WebCrawler.ProcessPage
  - after CheckMemoryUsage failed
  - after a cancellation request
  - after a crawl timeout
  - after CrawlDecision.ShouldHardStopCrawl in ShouldCrawlPageLinks / ShouldCrawlPage / ShouldRecrawlPage / ShouldDownloadPageContent
  - **scheduler.Clear**()
  - **threadManager.AbortAll**()
  - set all events to null so no more events are fired
- CheckForStopRequest()
  - if crawlContext.IsCrawlStopRequested
  - after CrawlDecision.ShouldStopCrawl in ShouldCrawlPageLinks / ShouldCrawlPage / ShouldRecrawlPage / ShouldDownloadPageContent
  - **scheduler.Clear**()

- if scheduler.Count > 0
- pageToCrawl = **scheduler.GetNext**()
- **threadManager.DoWork**(   =>  ProcessPage(pageToCrawl) )

### 3. Process one page

*WebCrawler.ProcessPage(PageToCrawl)*

*WebCrawler.CrawlThePage(PageToCrawl)*

*WebCrawler.SchedulePageLinks(CrawledPage)*

- crawledPage = await **pageRequester.MakeRequestAsync**(Uri, Func **shouldDownloadPageContent**)

*-- 2 ways to handle http redirects --*

- if **CrawlConfiguration.IsHttpRequestAutoRedirectsEnabled** && (crawledPage.HttpResponseMessage.RequestMessage.RequestUri.AbsoluteUri != crawledPage.Uri.AbsoluteUri)	
  - do nothing special, redirect was silently managed before

- if **not CrawlConfiguration.IsHttpRequestAutoRedirectsEnabled** && crawledPage.HttpResponseMessage.StatusCode >= 300 && crawledPage.HttpResponseMessage.StatusCode <= 399	
    - uri = ExtractRedirectUri(crawledPage);
    - page = new PageToCrawl(uri);
    - if **ShouldSchedulePageLink**(page)
    - **scheduler.Add**(page)

*-- Extract page links --*

- if **ShouldCrawlPageLinks**(crawledPage) || CrawlConfiguration.IsForcedLinkParsingEnabled
- crawledPage.ParsedLinks = **htmlParser.GetLinks**(crawledPage);

foreach (uri in crawledPage.ParsedLinks)

- if not **scheduler.IsUriKnown(hyperLink.HrefValue)** &&
- if  **ShouldScheduleLinkDecisionMaker**.Invoke(uri, crawledPage, crawlContext)		
- page = new PageToCrawl(uri);	
- page.IsInternal = **IsInternalUri**(uri)

- if **ShouldSchedulePageLink**(page)
   - **scheduler.Add**(page)
  
- **scheduler.AddKnownUri**(uri);		

return

- if **ShouldRecrawlPage**(crawledPage)	
- crawledPage.RetryAfter = seconds;
- crawledPage.IsRetry = true;
- **scheduler.Add**(crawledPage);

### 4. Decisions to limit the crawl scope

public interface ICrawlDecisionMaker
- CrawlDecision ShouldCrawlPage(PageToCrawl pageToCrawl, CrawlContext crawlContext);
- CrawlDecision ShouldDownloadPageContent(CrawledPage crawledPage, CrawlContext crawlContext);
- CrawlDecision ShouldCrawlPageLinks(CrawledPage crawledPage, CrawlContext crawlContext);
- CrawlDecision ShouldRecrawlPage(CrawledPage crawledPage, CrawlContext crawlContext);

public interface IWebCrawler
- Func<Uri, Uri, bool> IsInternalUriDecisionMaker { get; set; }
- Func<Uri, CrawledPage, CrawlContext, bool> ShouldScheduleLinkDecisionMaker { get; set; }
- Func<PageToCrawl, CrawlContext, CrawlDecision> ShouldCrawlPageDecisionMaker { get; set; }
- Func<CrawledPage, CrawlContext, CrawlDecision> ShouldDownloadPageContentDecisionMaker { get; set;}
- Func<CrawledPage, CrawlContext, CrawlDecision> ShouldCrawlPageLinksDecisionMaker { get; set; }
- Func<CrawledPage, CrawlContext, CrawlDecision> ShouldRecrawlPageDecisionMaker { get; set; }

#### ShouldSchedulePageLink

WebCrawler.ShouldSchedulePageLink(PageToCrawl page)		
- (page.IsInternal || CrawlConfiguration.IsExternalPageCrawlingEnabled) &&
- ShouldCrawlPage(page)

WebCrawler.IsInternalUri()		
- IsInternalUriDecisionMaker(uri, _crawlContext.RootUri) ||	
- IsInternalUriDecisionMaker(uri, _crawlContext.OriginalRootUri)	

WebCrawler.IsInternalUriDecisionMaker
- // Authority = Host Name + Port No
- (uriInQuestion, rootUri) => uriInQuestion.Authority == rootUri.Authority

WebCrawler.ShouldCrawlPage(PageToCrawl pageToCrawl)		
- decision = crawlDecisionMaker.ShouldCrawlPage(pageToCrawl, crawlContext) &&
- (ShouldCrawlPageDecisionMaker != null) ? ShouldCrawlPageDecisionMaker(pageToCrawl, _crawlContext)	
- crawlContext.IsCrawlHardStopRequested = decision.ShouldHardStopCrawl
- crawlContext.IsCrawlStopRequested = decision.ShouldStopCrawl

CrawlDecisionMaker.ShouldCrawlPage(PageToCrawl pageToCrawl, CrawlContext crawlContext)
- "HttpRequestMaxAutoRedirects limit of {CrawlConfiguration.HttpRequestMaxAutoRedirects} has been reached"
- "Crawl depth is above {CrawlConfiguration.MaxCrawlDepth}"
- "Scheme does not begin with http"
- "MaxPagesToCrawl limit of {CrawlConfiguration.MaxPagesToCrawl} has been reached"
- "MaxPagesToCrawlPerDomain limit of {CrawlConfiguration.MaxPagesToCrawlPerDomain} has been reached for domain {pageToCrawl.Uri.Authority}"
- "Link is external" (!pageToCrawl.IsInternal)

#### ShouldDownloadPageContent

WebCrawler.ShouldDownloadPageContent(CrawledPage crawledPage)
- decision = crawlDecisionMaker.ShouldDownloadPageContent(crawledPage, crawlContext) &&
- (ShouldDownloadPageContentDecisionMaker != null) ? ShouldDownloadPageContentDecisionMaker(crawledPage, crawlContext)
- crawlContext.IsCrawlHardStopRequested = decision.ShouldHardStopCrawl
- crawlContext.IsCrawlStopRequested = decision.ShouldStopCrawl

CrawlDecisionMaker.ShouldDownloadPageContent(CrawledPage crawledPage, CrawlContext crawlContext)
- "HttpStatusCode is not 200"
- "Content type is not any of the following: {CrawlConfiguration.DownloadableContentTypes}"
- "Page size of {HttpResponseMessage.Content.Headers.ContentLength} bytes is above the max allowable of {CrawlConfiguration.MaxPageSizeInBytes} bytes"

#### ShouldCrawlPageLinks

WebCrawler.ShouldCrawlPageLinks(crawledPage)		
- decision = crawlDecisionMaker.ShouldCrawlPageLinks(crawledPage, crawlContext) &&
- (ShouldCrawlPageLinksDecisionMaker != null) ? ShouldCrawlPageLinksDecisionMaker(crawledPage, crawlContext)
- if (!shouldCrawlPageLinksDecision.Allow) FirePageLinksCrawlDisallowedEvent(crawledPage)	
- crawlContext.IsCrawlHardStopRequested = decision.ShouldHardStopCrawl
- crawlContext.IsCrawlStopRequested = decision.ShouldStopCrawl

CrawlDecisionMaker.ShouldCrawlPageLinks(CrawledPage crawledPage, CrawlContext crawlContext)
- "Page has no content" (crawledPage.Content.Text)
- "Link is external" (!crawledPage.IsInternal)
- "Crawl depth is above {CrawlConfiguration.MaxCrawlDepth}"

#### ScheduleLinks

WebCrawler.SchedulePageLinks(CrawledPage crawledPage)
- foreach (var hyperLink in crawledPage.ParsedLinks)
   - if (!_scheduler.IsUriKnown(hyperLink.HrefValue) &&
   - (**ShouldScheduleLinkDecisionMaker** == null || **ShouldScheduleLinkDecisionMaker**(hyperLink.HrefValue, crawledPage, crawlContext)))
      - page = new PageToCrawl(hyperLink.HrefValue);
      - if (ShouldSchedulePageLink(page))
         - scheduler.Add(page);

#### ShouldRecrawlPage

WebCrawler.ShouldRecrawlPage(crawledPage)		
- decision = crawlDecisionMaker.ShouldRecrawlPage(crawledPage, crawlContext) &&
- (ShouldRecrawlPageDecisionMaker != null) ? ShouldRecrawlPageDecisionMaker(crawledPage, _crawlContext)
- if crawledPage.HttpResponseMessage?.Headers?.RetryAfter
- crawledPage.RetryAfter = (date - crawledPage.LastRequest.Value)
- crawlContext.IsCrawlHardStopRequested = decision.ShouldHardStopCrawl
- crawlContext.IsCrawlStopRequested = decision.ShouldStopCrawl

CrawlDecisionMaker.ShouldRecrawlPage(CrawledPage crawledPage, CrawlContext crawlContext)
- "WebException did not occur" (crawledPage.HttpRequestException == null)
- "MaxRetryCount is less than 1" (CrawlConfiguration.MaxRetryCount)
- "MaxRetryCount has been reached" (CrawlConfiguration.MaxRetryCount)

*WebCrawler.ProcessPage*

- FirePageCrawlCompletedEvent(crawledPage)
- if (ShouldRecrawlPage(crawledPage))
- crawledPage.IsRetry = true
- scheduler.Add(crawledPage)

*WebCrawler.CrawlThePage*

- FirePageCrawlStartingEvent(pageToCrawl)
- if (pageToCrawl.IsRetry)
- WaitMinimumRetryDelay(pageToCrawl)

### 5. Events activated during the crawl

public interface IWebCrawler
- event EventHandler<PageCrawlStartingArgs> PageCrawlStarting;
- event EventHandler<PageCrawlCompletedArgs> PageCrawlCompleted;
- event EventHandler<PageCrawlDisallowedArgs> PageCrawlDisallowed;
- event EventHandler<PageLinksCrawlDisallowedArgs> PageLinksCrawlDisallowed;

PageCrawlStarting

- Event that is fired before a page is crawled.
- PageToCrawl PageToCrawl
- CrawlContext CrawlContext

 PageCrawlCompleted

- Event that is fired when an individual page has been crawled.
- CrawledPage CrawledPage
- CrawlContext CrawlContext

PageCrawlDisallowed

- Event that is fired when the ICrawlDecisionMaker.ShouldCrawl impl returned false. This means the page or its links were not crawled.
- string DisallowedReason
- PageToCrawl PageToCrawl
- CrawlContext CrawlContext

PageLinksCrawlDisallowed

- Event that is fired when the ICrawlDecisionMaker.ShouldCrawlLinks impl returned false. This means the page's links were not crawled.
- string DisallowedReason
- CrawledPage CrawledPage
- CrawlContext CrawlContext

#### Event properties

**CrawlContext**

(1) Crawl config

- Uri RootUri : *The root of the crawl*
- Uri OriginalRootUri : *The root of the crawl specified in the configuration. If the root URI was redirected to another URI, it will be set in RootUri.*
- CrawlConfiguration CrawlConfiguration : *Configuration values used to determine crawl settings*

(2) Crawl stats

- DateTime CrawlStartDate : *Crawl starting date*
- int CrawledCount : *Total number of pages that have been crawled*
- ConcurrentDictionary<string, int> CrawlCountByDomain : *Threadsafe dictionary of domains and how many pages were crawled in that domain*
- int MemoryUsageBeforeCrawlInMb : *The memory usage in mb at the start of the crawl*
- int MemoryUsageAfterCrawlInMb : *The memory usage in mb at the end of the crawl*

(3) Scheduler state

- IScheduler Scheduler : *The scheduler that is being used*
- IsCrawlStopRequested : *Whether a request to stop the crawl has happened. Will clear all scheduled pages but will allow any threads that are currently crawling to complete.*
- IsCrawlHardStopRequested : * Whether a request to hard stop the crawl has happened. Will clear all scheduled pages and cancel any threads that are currently crawling.*
- CancellationTokenSource CancellationTokenSource : *Cancellation token used to hard stop the crawl. Will clear all scheduled pages and abort any threads that are currently crawling.*

(4) Custom data

- dynamic CrawlBag : *Random dynamic values - used to share user data during the crawl*

**PageToCrawl**

(1) Uri

- Uri Uri : *The uri of the page*
- bool IsRoot : *Whether the page is the root uri of the crawl*
- bool IsInternal : *Whether the page is internal to the root uri of the crawl*

(2) Crawl hierarchy

- Uri ParentUri : *The parent uri of the page*
- int CrawlDepth : *The depth from the root of the crawl. If this page is the homepage this value will be zero, if this page was found on the homepage this value will be 1 and so on.*

(3) Http Redirect (before)

- CrawledPage RedirectedFrom : *The uri that this page was redirected from. If null then it was not part of the redirect chain*
- int RedirectPosition : *The position in the redirect chain. The first redirect is position 1, the next one is 2 and so on.*

(4) Http Retries

- bool IsRetry : *Whether http requests had to be retried more than once. This could be due to throttling or politeness.*
- double? RetryAfter : *The time in seconds that the server sent to wait before retrying.*
- int RetryCount : *The number of times the http request was be retried.*
- DateTime? LastRequest : *The datetime that the last http request was made. Will be null unless retries are enabled.*

(5) Custom data

- dynamic PageBag : *an store values of any type. Useful for adding custom values to the CrawledPage dynamically from event subscriber code*

**CrawledPage**

(0) PageToCrawl properties, and in addition :

(1) Http Request -> Response

- HttpRequestMessage HttpRequestMessage : *Web request sent to the server.*
- HttpResponseMessage HttpResponseMessage : *Web response from the server.*
- HttpRequestException HttpRequestException : *The request exception that occurred during the request*
- HttpClientHandler HttpClientHandler : *The HttpClientHandler that was used to make the request to server*

(2) Request and download duration

- DateTime RequestStarted : *A datetime of when the http request started*
- DateTime RequestCompleted : *A datetime of when the http request completed*
- double Elapsed : *Time it took from RequestStarted to RequestCompleted in milliseconds*
- DateTime? DownloadContentStarted : *A datetime of when the page content download started, this may be null if downloading the content was disallowed by the CrawlDecisionMaker or the inline delegate ShouldDownloadPageContent*
- DateTime? DownloadContentCompleted : *A datetime of when the page content download completed, this may be null if downloading the content was disallowed by the CrawlDecisionMaker or the inline delegate ShouldDownloadPageContent*

(3) Page content : text, HTML, links

- PageContent Content : *The content of page request*
- IHtmlDocument AngleSharpHtmlDocument : *Lazy loaded AngleSharp IHtmlDocument (https://github.com/AngleSharp/AngleSharp) that can be used to retrieve/modify html elements on the crawled page.*
- IEnumerable<HyperLink> ParsedLinks : *Links parsed from page. This value is set by the WebCrawler.SchedulePageLinks() method only If the "ShouldCrawlPageLinks" rules return true or if the IsForcedLinkParsingEnabled config value is set to true.*

InitializeAngleSharpHtmlParser()
- angleSharpHtmlParser = new AngleSharp.Html.Parser.HtmlParser()
- angleSharpHtmlParser.ParseDocument(Content.Text)

(4) Http Redirect (after)

- PageToCrawl RedirectedTo : *The page that this pagee was redirected to*

**PageContent**

- byte[] Bytes : *The raw data bytes taken from the web response*
- string Charset : *String representation of the charset/encoding*
- Encoding Encoding : *The encoding of the web response*
- string Text : *The raw text taken from the web response*

### 6. IPageRequester / PageRequester

public interface IPageRequester 
- Task<CrawledPage> MakeRequestAsync(Uri uri)
- Task<CrawledPage> MakeRequestAsync(Uri uri, Func<CrawledPage, CrawlDecision> shouldDownloadContent)

*pageRequester = pageRequester ?? new PageRequester(crawlContext.CrawlConfiguration, new WebContentExtractor())*

PageRequester.PageRequester(CrawlConfiguration config, IWebContentExtractor contentExtractor, **HttpClient httpClient** = null)
- ServicePointManager.DefaultConnectionLimit = config.HttpServicePointConnectionLimit;
- if (httpClient == null)
- httpClientHandler = new HttpClientHandler()
- httpClientHandler.MaxAutomaticRedirections = config.HttpRequestMaxAutoRedirects
- httpClientHandler.UseDefaultCredentials = config.UseDefaultCredentials
- if (config.IsHttpRequestAutomaticDecompressionEnabled)
- httpClientHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
- if (config.HttpRequestMaxAutoRedirects > 0)
- httpClientHandler.AllowAutoRedirect = config.IsHttpRequestAutoRedirectsEnabled;
- if (config.IsSendingCookiesEnabled)
- httpClientHandler.CookieContainer = _cookieContainer;
- httpClientHandler.UseCookies = true;
- if (!config.IsSslCertificateValidationEnabled)
- httpClientHandler.ServerCertificateCustomValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
- if (config.IsAlwaysLogin && rootUri != null)
- var cache = new CredentialCache();
- cache.Add(new Uri($"http://{rootUri.Host}"), "Basic", new NetworkCredential(config.LoginUser, config.LoginPassword));
- cache.Add(new Uri($"https://{rootUri.Host}"), "Basic", new NetworkCredential(config.LoginUser, config.LoginPassword));
- httpClientHandler.Credentials = cache;
- httpClient = new HttpClient(clientHandler);
- httpClient.DefaultRequestHeaders.Add("User-Agent", _config.UserAgentString);
- httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
- if (config.HttpRequestTimeoutInSeconds > 0)
- httpClient.Timeout = config.HttpRequestTimeoutInSeconds
- if (config.IsAlwaysLogin)
- httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + credentials)

*crawledPage = await pageRequester.MakeRequestAsync(pageToCrawl.Uri, shouldDownloadPageContent)*

PageRequester.MakeRequestAsync(Uri uri, Func<CrawledPage, CrawlDecision> shouldDownloadContent)
- crawledPage = new CrawledPage(uri);										
- requestMessage = BuildHttpRequestMessage(uri)										
- response = await httpClient.SendAsync(requestMessage)	
- => crawledPage.RequestStarted = … / crawledPage.RequestCompleted = …
- crawledPage.HttpRequestException = …										
- crawledPage.HttpRequestMessage = …										
- crawledPage.HttpResponseMessage = …										
- if shouldDownloadContent(crawledPage)
- crawledPage.Content = await contentExtractor.GetContentAsync(response)
- => crawledPage.DownloadContentStarted = …	/ crawledPage.DownloadContentCompleted = …							
											
WebContentExtractor.GetContentAsync(HttpResponseMessage response)											
- pageContent.Bytes = …										
- pageContent.Charset = GetCharsetFromHeaders / GetCharsetFromBody / CleanCharset										
- pageContent.Encoding = … 										
- pageContent.Text = …															

### 7. IHtmlParser / AngleSharpHyperlinkParser

CrawledPage.AngleSharpHtmlDocument
- = new Lazy<IHtmlDocument>(InitializeAngleSharpHtmlParser)

CrawledPage.InitializeAngleSharpHtmlParser()								
- angleSharpHtmlParser = new HtmlParser();							
- document = angleSharpHtmlParser.ParseDocument(Content.Text)

public interface IHtmlParser
- IEnumerable<HyperLink> GetLinks(CrawledPage crawledPage)

**htmlParser = htmlParser ?? new AngleSharpHyperlinkParser(crawlContext.CrawlConfiguration, null)*								
								
HyperLinkParser(CrawlConfiguration config, Func<string, string> cleanUrlFunc)								
								
HyperLinkParser.GetLinks(crawledPage)
- if (HasRobotsNoFollow(crawledPage)) return null;
- rawHyperLinks = crawledPage.AngleSharpHtmlDocument.QuerySelectorAll("a…							
- // Use the uri of the page that actually responded to the request instead of crawledPage.Uri							
- // If html base tag exists use it instead of page uri for relative links							
- // Remove the url fragment part of the url if needed							
- CleanUrlFunc(new Uri(uriToUse, href).AbsoluteUri))							
- finalList.Add( new HyperLink { RawHrefValue = rawLink.RawHrefValue, RawHrefText = rawLink.RawHrefText, HrefValue = uriValueToUse });							

### 8. IThreadManager / TaskTreadManager

Each page is processed by a distinct thread until MaxThreads :
- if CrawlConfiguration.MaxConcurrentThreads > 0 : MaxThreads = crawlContext.CrawlConfiguration.MaxConcurrentThreads
- else : MaxThreads = Environment.ProcessorCount

*threadManager.DoWork(async () => await ProcessPage(scheduler.GetNext()));*

public interface IThreadManager
- int MaxThreads
- void DoWork(Action action)
- bool HasRunningThreads()
- void AbortAll();

*if MaxThreads > 1*

- numberOfRunningThreads++;
- if (numberOfRunningThreads >= MaxThreads)
- wait until the end of last action ...

TaskThreadManager.RunActionOnDedicatedThread(Action action)
- Task.Factory
- .StartNew(() => RunAction(action), _cancellationTokenSource.Token)
- .ContinueWith(HandleAggregateExceptions, TaskContinuationOptions.OnlyOnFaulted);

TaskThreadManager.HandleAggregateExceptions(Task task)
- aggException = task.Exception.Flatten();
- foreach (var exception in aggException.InnerExceptions)
- Log.Error("Aggregate Exception: {0}", exception)

### 9. PoliteWebCrawler

public interface IPoliteWebCrawler : IWebCrawler
- event EventHandler<RobotsDotTextParseCompletedArgs> RobotsDotTextParseCompleted;

public PoliteWebCrawler(... , IDomainRateLimiter domainRateLimiter, IRobotsDotTextFinder robotsDotTextFinder)
- domainRateLimiter = domainRateLimiter ?? new DomainRateLimiter(crawlContext.CrawlConfiguration.MinCrawlDelayPerDomainMilliSeconds);
- robotsDotTextFinder = robotsDotTextFinder ?? new RobotsDotTextFinder(new PageRequester(crawlContext.CrawlConfiguration, new WebContentExtractor()));

PoliteWebCrawler.CrawlAsync(Uri uri, CancellationTokenSource cancellationTokenSource)
- robotsDotText = await_robotsDotTextFinder.FindAsync(uri)
- FireRobotsDotTextParseCompleted(_robotsDotText.Robots);
- domainRateLimiter.AddDomain(uri, robotsDotTextCrawlDelayInMillisecs);
- PageCrawlStarting += (s, e) => _domainRateLimiter.RateLimit(e.PageToCrawl.Uri);

public class RobotsDotText : IRobotsDotText
- robotsDotTextUtil = new Robots.Robots(); [[NRobotsCore package]]
- robotsDotTextUtil.LoadContent(content, rootUri.AbsoluteUri);
- int GetCrawlDelay(string userAgentString)
- public bool IsUrlAllowed(string url, string userAgentString)
- bool IsUserAgentAllowed(string userAgentString)
- IRobots Robots { get { return _robotsDotTextUtil; } }

RateLimiter.WaitToProceed(int millisecondsTimeout)
- // Block until we can enter the semaphore or until the timeout expires.
- entered = _emaphore.Wait(millisecondsTimeout);
- // If we entered the semaphore, compute the corresponding exit time and add it to the queue.
- if (entered)
- timeToExit = unchecked(Environment.TickCount + TimeUnitMilliseconds)
- exitTimes.Enqueue(timeToExit)

PoliteWebCrawler.ShouldCrawlPage(PageToCrawl pageToCrawl)
- allowedByRobots = true;
- allowedByRobots = robotsDotText.IsUrlAllowed(pageToCrawl.Uri.AbsoluteUri, crawlContext.CrawlConfiguration.RobotsDotTextUserAgentString)
- if (CrawlConfiguration.IsIgnoreRobotsDotTextIfRootDisallowedEnabled && pageToCrawl.IsRoot)    
   - if (!allowedByRobots) || (!allPathsBelowRootAllowedByRobots)
   - allowedByRobots = true;
- if (!allowedByRobots)
   - FirePageCrawlDisallowedEvent(pageToCrawl, message);
- return allowedByRobots && base.ShouldCrawlPage(pageToCrawl)

### 10. Configuration properties

**CrawlConfiguration**

(1) Stopping conditions

- int CrawlTimeoutSeconds : *Maximum seconds before the crawl times out and stops. If zero, this setting has no effect.*
- int MaxPagesToCrawl : *Maximum number of pages to crawl. If zero, this setting has no effect*
- int MaxPagesToCrawlPerDomain : *Maximum number of pages to crawl per domain. If zero, this setting has no effect.*

(2) Crawl scope

- bool IsExternalPageCrawlingEnabled : *Whether pages external to the root uri should be crawled*
- int MaxCrawlDepth : *Maximum levels below root page to crawl. If value is 0, the homepage will be crawled but none of its links will be crawled. If the level is 1, the homepage and its links will be crawled but none of the links links will be crawled.*
- int MaxPageSizeInBytes : *Maximum size of page. If the page size is above this value, it will not be downloaded or processed. If zero, this setting has no effect.*
---
- bool IsExternalPageLinksCrawlingEnabled : *Whether pages external to the root uri should have their links crawled. NOTE: IsExternalPageCrawlEnabled must be true for this setting to have any effect*
- bool IsForcedLinkParsingEnabled : *Gets or sets a value that indicates whether the crawler should parse the page's links even if a CrawlDecision (like CrawlDecisionMaker.ShouldCrawlPageLinks()) determines that those links will not be crawled.*
- int MaxLinksPerPage : *Maximum links to crawl per page. If value is zero, this setting has no effect.*
---
- bool IsRespectUrlNamedAnchorOrHashbangEnabled : *Whether or not url named anchors or hashbangs are considered part of the url. If false, they will be ignored. If true, they will be considered part of the url.*
- string DownloadableContentTypes : *A comma separated string that has content types that should have their page content downloaded. For each page, the content type is checked to see if it contains any of the values defined here.*

(3) Parallelism

- int MaxConcurrentThreads : *Max concurrent threads to use for http requests*
- int HttpServicePointConnectionLimit : *Gets or sets the maximum number of concurrent connections allowed by a System.Net.ServicePoint. The system default is 2. This means that only 2 concurrent http connections can be open to the same host. If zero, this setting has no effect.*

(4) Crawl behavior

- bool IsHttpRequestAutoRedirectsEnabled : *Gets or sets a value that indicates whether the request should follow redirection*
- int HttpRequestMaxAutoRedirects : *Gets or sets the maximum number of redirects that the request follows. If zero, this setting has no effect.*
---
- int MaxRetryCount : *The max number of retries for a url if a web exception is encountered. If the value is 0, no retries will be made*
- int MinRetryDelayInMilliseconds : *The minimum delay between a failed http request and the next retry*
---
- bool IsUriRecrawlingEnabled : *Whether Uris should be crawled more than once. This is not common and should be false for most scenarios*

(5) Browser emulation

- string UserAgentString : *The user agent string to use for http requests*
---
- HttpProtocolVersion HttpProtocolVersion : *The http protocol version number to use during http requests. Currently supporting values "1.1" and "1.0". *
- int HttpRequestTimeoutInSeconds : *Gets or sets the time-out value in seconds for the System.Net.HttpWebRequest.GetResponse() and System.Net.HttpWebRequest.GetRequestStream() methods. If zero, this setting has no effect.*
- bool IsSslCertificateValidationEnabled : *Whether or not to validate the server SSL certificate. If true, the default validation will be made. If false, the certificate validation is bypassed. This setting is useful to crawl sites with an invalid or expired SSL certificate.*
- bool IsHttpRequestAutomaticDecompressionEnabled : *Gets or sets a value that indicates gzip and deflate will be automatically accepted and decompressed*
---
- bool IsSendingCookiesEnabled : *Whether the cookies should be set and resent with every request*
---
- bool IsAlwaysLogin : *Defines whether each request should be authorized via login*
- string LoginUser : *The user name to be used for authorization*
- string LoginPassword : *The password to be used for authorization*
- bool UseDefaultCredentials : *Specifies whether to use default credentials.*

(6) Politeness

- bool IsRespectRobotsDotTextEnabled : *Whether the crawler should retrieve and respect the robots.txt file.*
- string RobotsDotTextUserAgentString : *The user agent string to use when checking robots.txt file for specific directives.  Some examples of other crawler's user agent values are "googlebot", "slurp" etc...*
- bool IsRespectMetaRobotsNoFollowEnabled : *Whether the crawler should ignore links on pages that have a <meta name="robots" content="nofollow" /> tag*
- bool IsRespectHttpXRobotsTagHeaderNoFollowEnabled : *Whether the crawler should ignore links on pages that have an http X-Robots-Tag header of nofollow*
- bool IsRespectAnchorRelNoFollowEnabled : *Whether the crawler should ignore links that have a <a href="whatever" rel="nofollow" />...*
- bool IsIgnoreRobotsDotTextIfRootDisallowedEnabled : *If true, will ignore the robots.txt file if it disallows crawling the root uri.*
---
- int MinCrawlDelayPerDomainMilliSeconds : *The number of milliseconds to wait in between http requests to the same domain.*
- int MaxRobotsDotTextCrawlDelayInSeconds : *The maximum numer of seconds to respect in the robots.txt "Crawl-delay: X" directive. IsRespectRobotsDotTextEnabled must be true for this value to be used. If zero, will use whatever the robots.txt crawl delay requests no matter how high the value is.*

(7) Memory resources

- int MaxMemoryUsageInMb : *The max amount of memory to allow the process to use. If this limit is exceeded the crawler will stop prematurely. If zero, this setting has no effect.*
- int MinAvailableMemoryRequiredInMb : *Uses closest multiple of 16 to the value set. If there is not at least this much memory available before starting a crawl, throws InsufficientMemoryException. If zero, this setting has no effect.*
- int MaxMemoryUsageCacheTimeInSeconds : *The max amount of time before refreshing the value used to determine the amount of memory being used by the process that hosts the crawler instance. This value has no effect if MaxMemoryUsageInMb is zero.*

(8) Custom config properties

- Dictionary<string, string> ConfigurationExtensions : *Dictionary that stores additional key-value pairs that can be accessed through the crawl pipeline*