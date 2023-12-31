﻿using Abot.Poco;
using System;
using System.Collections.Concurrent;
namespace Abot.Core
{
    public interface IPagesToCrawlRepository : IDisposable
    {
        void Add(PageToCrawl page);
        PageToCrawl GetNext();
        void Clear();
        int Count();

    }

    public class FifoPagesToCrawlRepository : IPagesToCrawlRepository
    {
        private ConcurrentQueue<PageToCrawl> _urlQueue = new ConcurrentQueue<PageToCrawl>();

        public ConcurrentQueue<PageToCrawl> UrlQueue { get { return _urlQueue; } set { _urlQueue = value; } }

        public FifoPagesToCrawlRepository() { }

        public void Add(PageToCrawl page)
        {
            _urlQueue.Enqueue(page);
        }

        public PageToCrawl GetNext()
        {
            PageToCrawl pageToCrawl;
            _urlQueue.TryDequeue(out pageToCrawl);

            return pageToCrawl;
        }

        public void Clear()
        {
            _urlQueue = new ConcurrentQueue<PageToCrawl>();
        }

        public int Count()
        {
            return _urlQueue.Count;
        }

        public virtual void Dispose()
        {
            _urlQueue = null;
        }
    }

}
