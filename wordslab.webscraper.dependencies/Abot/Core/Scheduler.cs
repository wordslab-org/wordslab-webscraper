using Abot.Poco;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;

namespace Abot.Core
{
    /// <summary>
    /// Handles managing the priority of what pages need to be crawled
    /// </summary>
    public interface IScheduler : IDisposable
    {
        /// <summary>
        /// Count of remaining items that are currently scheduled
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Schedules the param to be crawled
        /// </summary>
        void Add(PageToCrawl page);

        /// <summary>
        /// Schedules the param to be crawled
        /// </summary>
        void Add(IEnumerable<PageToCrawl> pages);

        /// <summary>
        /// Gets the next page to crawl
        /// </summary>
        PageToCrawl GetNext();

        /// <summary>
        /// Clear all currently scheduled pages
        /// </summary>
        void Clear();

        /// <summary>
        /// Add the Url to the list of crawled Url without scheduling it to be crawled.
        /// </summary>
        /// <param name="uri"></param>
        void AddKnownUri(Uri uri);

        /// <summary>
        /// Returns whether or not the specified Uri was already scheduled to be crawled or simply added to the
        /// list of known Uris.
        /// </summary>
        bool IsUriKnown(Uri uri);

        /// <summary>
        /// Dictonary tracking all the unique text blocks encountered so far during the crawl.
        /// Keys: Fast and unreliable 32 bits hash of the text block
        /// Values: number of words of the corresponding text block
        /// </summary>
        ConcurrentDictionary<int,int> UniqueTextBlocks { get; }
    }

    public class Scheduler : IScheduler
    {
        bool _allowUriRecrawling;
        ICrawledUrlRepository _crawledUrlRepo;
        IPagesToCrawlRepository _pagesToCrawlRepo;
        ConcurrentDictionary<int, int> _uniqueTextBlocks;

        public ConcurrentDictionary<int, int> UniqueTextBlocks { get { return _uniqueTextBlocks; } }

        public Scheduler()
            :this(false, null, null, null)
        {
        }

        public Scheduler(bool allowUriRecrawling, ICrawledUrlRepository crawledUrlRepo, IPagesToCrawlRepository pagesToCrawlRepo, ConcurrentDictionary<int, int> uniqueTextBlocks)
        {
            _allowUriRecrawling = allowUriRecrawling;
            _crawledUrlRepo = crawledUrlRepo ?? new CompactCrawledUrlRepository();
            _pagesToCrawlRepo = pagesToCrawlRepo ?? new FifoPagesToCrawlRepository();
            _uniqueTextBlocks = uniqueTextBlocks ?? new ConcurrentDictionary<int, int>();
        }

        public void Serialize(string extractionStateDir)
        {
            var (crawledUrlsPath,pagesToCrawlPath,uniqueTextBlocksPath) = GetSerializationFilesNames(extractionStateDir);
                        
            var jsonString = JsonSerializer.Serialize(_crawledUrlRepo);
            File.WriteAllText(crawledUrlsPath, jsonString);

            jsonString = JsonSerializer.Serialize(_pagesToCrawlRepo);
            File.WriteAllText(pagesToCrawlPath, jsonString);

            jsonString = JsonSerializer.Serialize(_uniqueTextBlocks);
            File.WriteAllText(uniqueTextBlocksPath, jsonString);
        }

        public static Scheduler Deserialize(bool allowUriRecrawling, string extractionStateDir)
        {
            var (crawledUrlsPath, pagesToCrawlPath, uniqueTextBlocksPath) = GetSerializationFilesNames(extractionStateDir);
            
            var jsonString = File.ReadAllText(crawledUrlsPath);
            var crawledUrlRepo = JsonSerializer.Deserialize<CompactCrawledUrlRepository>(jsonString);

            jsonString = File.ReadAllText(pagesToCrawlPath);
            var pagesToCrawlRepo = JsonSerializer.Deserialize<FifoPagesToCrawlRepository>(jsonString);

            jsonString = File.ReadAllText(uniqueTextBlocksPath);
            var  uniqueTextBlocks = JsonSerializer.Deserialize<ConcurrentDictionary<int, int>>(jsonString);

            return new Scheduler(allowUriRecrawling, crawledUrlRepo, pagesToCrawlRepo, uniqueTextBlocks);
        }

        private static (string,string,string) GetSerializationFilesNames(string extractionStateDir)
        {
            return (
                Path.Combine(extractionStateDir, "crawledUrls.json"), 
                Path.Combine(extractionStateDir, "pagesToCrawl.json"),
                Path.Combine(extractionStateDir, "uniqueTextBlocks.json")
            );
        }

        public delegate bool PageFilter(PageToCrawl pageToCrawl);

        public void FilterAllowedUrlsAfterConfig(PageFilter shouldCrawlPage)
        {
            // The Scheduler was deserialized after a 'continue' command
            if(_pagesToCrawlRepo?.Count()  > 0)
            {
                var initialRepo = _pagesToCrawlRepo;
                _pagesToCrawlRepo = new FifoPagesToCrawlRepository();
                PageToCrawl candidatePage;
                while ((candidatePage = initialRepo.GetNext()) != null)
                {
                    if(shouldCrawlPage(candidatePage))
                    {
                        _pagesToCrawlRepo.Add(candidatePage);
                    }
                }
            }            
        }

        public int Count
        {
            get { return _pagesToCrawlRepo.Count(); }
        }

        public void Add(PageToCrawl page)
        {
            if (page == null)
                throw new ArgumentNullException("page");

            if (_allowUriRecrawling || page.IsRetry)
            {
                _pagesToCrawlRepo.Add(page);
            }
            else
            {
                if (_crawledUrlRepo.AddIfNew(page.Uri))
                    _pagesToCrawlRepo.Add(page);
            }
        }

        public void Add(IEnumerable<PageToCrawl> pages)
        {
            if (pages == null)
                throw new ArgumentNullException("pages");

            foreach (PageToCrawl page in pages)
                Add(page);
        }

        public PageToCrawl GetNext()
        {
            return _pagesToCrawlRepo.GetNext();
        }

        public void Clear()
        {
            _pagesToCrawlRepo.Clear();
        }

        public void AddKnownUri(Uri uri)
        {
            _crawledUrlRepo.AddIfNew(uri);
        }

        public bool IsUriKnown(Uri uri)
        {
            return _crawledUrlRepo.Contains(uri);
        }

        public void Dispose()
        {
            if (_crawledUrlRepo != null)
            {
                _crawledUrlRepo.Dispose();
            }
            if (_pagesToCrawlRepo != null)
            {
                _pagesToCrawlRepo.Dispose();
            }
        }
    }
}
