﻿#region License and copyright notice
/* 
 * Kaliko Content Management System
 * 
 * Copyright (c) Fredrik Schultz
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3.0 of the License, or (at your option) any later version.
 * 
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 * http://www.gnu.org/licenses/lgpl-3.0.html
 */
#endregion

namespace KalikoCMS {
    using System;
    using System.Collections.Generic;
    using System.Web;
    using Data.Entities;
    using Kaliko;
    using ContentProvider;
    using Core;
    using Core.Collections;
    using Data;
    using Events;
    using Search;

    public class PageFactory {
        private static List<PageIndex> _pageLanguageIndex;
        private static bool _indexing;
        private static PageEventHandler _pageSaved;
        private static PageEventHandler _pageDeleted;


        private static PageIndex CurrentIndex {
            get {
                return _pageLanguageIndex.Find(i => i.LanguageId == Language.CurrentLanguageId);
            }
        }


        public static bool FindPage(string pageUrl, IRequestManager requestManager) {
            if (_pageLanguageIndex == null) {
                IndexSite();
            }

            var pageIndex = GetPageIndex(Language.CurrentLanguageId);

            if (pageIndex.Items.Count == 0) {
                return false;
            }

            var segments = GetUrlSegments(pageUrl);
            var position = 0;
            var lastPage = new PageIndexItem();

            for (var i = 0; i < segments.Length; i++) {
                var segment = segments[i];
                var segmentHash = segment.GetHashCode();

                while (true) {
                    var page = pageIndex.Items[position];
                    if ((page.UrlSegmentHash == segmentHash) && (page.UrlSegment == segment)) {
                        if (i == segments.Length - 1) {
                            requestManager.HandlePage(page);
                            return true;
                        }

                        lastPage = page;
                        position = page.FirstChild;

                        if (position == -1) {
                            if (TryAsPageExtender(i + 1, segments, lastPage)) {
                                return true;
                            }
                            if (requestManager.TryMvcSupport(i, segments, lastPage)) {
                                return true;
                            }
                            if (TryAsRedirect(pageUrl)) {
                                return true;
                            }

                            return false;
                        }

                        // Continue to next segment
                        break;
                    }

                    position = page.NextPage;

                    if (position == -1) {
                        if (TryAsPageExtender(i, segments, lastPage)) {
                            return true;
                        }
                        if (requestManager.TryMvcSupport(i, segments, lastPage)) {
                            return true;
                        }
                        if (TryAsRedirect(pageUrl)) {
                            return true;
                        }

                        return false;
                    }
                }
            }



            return false;
        }

        public static Guid GetPageIdFromUrl(string url) {
            if (_pageLanguageIndex == null)
                IndexSite();

            var pageIndex = GetPageIndex(Language.CurrentLanguageId);

            if (pageIndex.Items.Count == 0) {
                return Guid.Empty;
            }

            var segments = GetUrlSegments(url);
            var position = 0;

            for (var i = 0; i < segments.Length; i++) {
                var segment = segments[i];
                var segmentHash = segment.GetHashCode();

                while (true) {
                    var page = pageIndex.Items[position];
                    if ((page.UrlSegmentHash == segmentHash) && (page.UrlSegment == segment)) {
                        if (i == segments.Length - 1) {
                            return page.PageId;
                        }

                        position = page.FirstChild;

                        if (position == -1) {
                            return Guid.Empty;
                        }

                        break;
                    }

                    position = page.NextPage;

                    if (position == -1) {
                        return Guid.Empty;
                    }
                }
            }

            return Guid.Empty;
        }

        private static string[] GetUrlSegments(string url) {
            if (url.EndsWith(".aspx", StringComparison.InvariantCultureIgnoreCase)) {
                url = url.Substring(0, url.Length - 5);
            }

            return url.Trim('/').Split('/');
        }


        private static bool TryAsPageExtender(int i, string[] segments, PageIndexItem page) {
            var pageType = PageType.GetPageType(page.PageTypeId);
            if (pageType == null) {
                return false;
            }

            var valueSupport = pageType.Instance as IPageExtender;

            if (valueSupport == null) {
                return false;
            }
            
            var remainingSegments = new string[segments.Length - i];
            Array.Copy(segments, i, remainingSegments, 0, remainingSegments.Length);

            return valueSupport.HandleRequest(page.PageId, remainingSegments);
        }

        private static bool TryAsRedirect(string pageUrl) {
            var page = RedirectManager.GetPageForPreviousUrl(pageUrl);
            if (page == null) {
                return false;
            }

            var response = HttpContext.Current.Response;
            response.Status = "301 Moved Permanently";
            response.AddHeader("Location", page.PageUrl.ToString());
            response.End();

            return true;
        }

        public static PageCollection GetChildrenForPage(Guid pageId, PublishState pageState = PublishState.Published) {
            var pageIndex = CurrentIndex;

            if (pageId == Guid.Empty) {
                return pageIndex.GetRootChildren(pageState);
            }
            
            return pageIndex.GetChildren(pageId, pageState);
        }


        public static PageCollection GetChildrenForPage(Guid pageId, Predicate<PageIndexItem> match) {
            return CurrentIndex.GetChildrenByCriteria(pageId, match);
        }


        public static PageCollection GetChildrenForPageOfPageType(Guid pageId, int pageTypeId, PublishState pageState = PublishState.Published) {
            if (pageId == Guid.Empty) {
                return CurrentIndex.GetRootChildren(pageTypeId, pageState);
            }
            
            return CurrentIndex.GetChildren(pageId, pageTypeId, pageState);
        }


        public static PageCollection GetChildrenForPageOfPageType(Guid pageId, Type pageType, PublishState pageState = PublishState.Published) {
            var pageTypeItem = PageType.GetPageType(pageType);

            return GetChildrenForPageOfPageType(pageId, pageTypeItem.PageTypeId, pageState);
        }


        public static CmsPage GetPage(Guid pageId) {
            return GetPage(pageId, Language.CurrentLanguageId);
        }


        public static CmsPage GetPage(Guid pageId, int languageId) {
            if (pageId == Guid.Empty) {
                return new RootPage(languageId);
            }

            var pageIndexItem = GetPageIndexItem(pageId, languageId);

            if (pageIndexItem == null) {
                return null;
            }
            
            return new CmsPage(pageIndexItem, languageId);
        }


        public static T GetPage<T>(Guid pageId) where T : CmsPage {
            return GetPage<T>(pageId, Language.CurrentLanguageId);
        }


        public static T GetPage<T>(Guid pageId, int languageId) where T : CmsPage {
            var page = GetPage(pageId, Language.CurrentLanguageId);
            return page.ConvertToTypedPage<T>();
        }


        public static PageCollection GetPagePath(CmsPage page) {
            return GetPagePath(page.PageId, page.LanguageId);
        }


        public static PageCollection GetPagePath(Guid pageId) {
            var languageId = Language.CurrentLanguageId;
            return GetPagePath(pageId, languageId);
        }


        private static PageCollection GetPagePath(Guid pageId, int languageId) {
            var pageIndex = GetPageIndex(languageId);
            return pageIndex.GetPagePath(pageId);
        }


        public static CmsPage GetParentAtLevel(Guid pageId, int level) {
            var pageCollection = GetPagePath(pageId);
            level++;

            if (pageCollection.Count < level) {
                return null;
            }

            var pageCount = pageCollection.Count;
            var parentId = pageCollection.PageIds[pageCount - level];
            var page = GetPage(parentId);

            return page;
        }


        public static PageCollection GetPageTreeFromPage(Guid pageId, PublishState pageState) {
            return CurrentIndex.GetPageTreeFromPage(pageId, pageState);
        }


        public static PageCollection GetPageTreeFromPage(Guid pageId, Predicate<PageIndexItem> match) {
            return CurrentIndex.GetPageTreeFromPage(pageId, match);
        }
        
        
        public static PageCollection GetPageTreeFromPage(Guid rootPageId, Guid leafPageId, PublishState pageState) {
            return CurrentIndex.GetPageTreeFromPage(rootPageId, leafPageId, pageState);
        }


        internal static void IndexSite() {
            if (!_indexing) {
                _indexing = true;

                try {
                    TagManager.ClearCache();

                    if (_pageLanguageIndex != null) {
                        _pageLanguageIndex.Clear();
                    }

                    _pageLanguageIndex = new List<PageIndex>();

                    var languages = Language.Languages;

                    foreach (var language in languages) {
                        IndexSite(language.LanguageId);
                    }
                }
                catch (Exception e) {
                    Logger.Write("Indexing failed!! " + e.Message, Logger.Severity.Critical);
                    throw;
                }
                finally {
                    _indexing = false;
                }
            }
            else {
                // TODO: Fin sida med felmeddelande här kanske..? :)
                HttpContext.Current.Response.Clear();
                Utils.RenderSimplePage(HttpContext.Current.Response, "Reindexing the site..", "Please check back in 10 seconds..");
            }
        }


        internal static void RaisePageSaved(Guid pageId, int languageId) {
            if (_pageSaved != null) {
                _pageSaved(null, new PageEventArgs(pageId, languageId));
            }
        }


        internal static void RaisePageDeleted(Guid pageId, int languageId) {
            if (_pageDeleted != null) {
                _pageDeleted(null, new PageEventArgs(pageId, languageId));
            }
        }

        internal static void UpdatePageIndex(PageInstanceEntity pageInstance, Guid parentId, Guid rootId, int treeLevel, int pageTypeId, int sortOrder) {
            if (_pageLanguageIndex == null)
                IndexSite();

            var pageIndex = GetPageIndex(pageInstance.LanguageId);
            var page = pageIndex.GetPageIndexItem(pageInstance.PageId);

            if (page != null) {
                page.PageName = pageInstance.PageName;
                page.UpdateDate = pageInstance.UpdateDate;
                page.StartPublish = pageInstance.StartPublish;
                page.StopPublish = pageInstance.StopPublish;
                page.VisibleInMenu = pageInstance.VisibleInMenu;
                page.VisibleInSiteMap = pageInstance.VisibleInSitemap;
                pageIndex.SavePageIndexItem(page);
            }
            else {
                page = new PageIndexItem {
                                             Author = pageInstance.Author,
                                             CreatedDate = pageInstance.CreatedDate,
                                             DeletedDate = pageInstance.DeletedDate,
                                             FirstChild = -1,
                                             NextPage = -1,
                                             PageId = pageInstance.PageId,
                                             PageInstanceId = pageInstance.PageInstanceId,
                                             PageName = pageInstance.PageName,
                                             PageTypeId = pageTypeId,
                                             PageUrl = BuildPageUrl(pageInstance, parentId),
                                             ParentId = parentId,
                                             RootId = rootId,
                                             SortOrder = sortOrder,
                                             StartPublish = pageInstance.StartPublish,
                                             StopPublish = pageInstance.StopPublish,
                                             VisibleInMenu = pageInstance.VisibleInMenu,
                                             VisibleInSiteMap = pageInstance.VisibleInSitemap,
                                             UpdateDate = pageInstance.UpdateDate,
                                             UrlSegment = pageInstance.PageUrl
                                         };
                page.UrlSegmentHash = page.UrlSegment.GetHashCode();
                page.TreeLevel = treeLevel;

                pageIndex.InsertPageIndexItem(page);
            }
        }


        private static string BuildPageUrl(PageInstanceEntity pageInstance, Guid parentId) {
            var parent = GetPage(parentId);
            var parentUrl = parent.PageUrl.ToString();
            var url = string.Format("{0}{1}/", parentUrl, pageInstance.PageUrl);
            url = url.TrimStart('/');

            return url;
        }


        private static PageIndex GetPageIndex(int languageId) {
            return _pageLanguageIndex.Find(i => i.LanguageId == languageId);
        }


        private static PageIndexItem GetPageIndexItem(Guid pageId, int languageId) {
            if (_pageLanguageIndex == null)
                IndexSite();

            var pageIndex = GetPageIndex(languageId);

            if ((pageIndex == null) || (pageIndex.Count < 1)) {
                IndexSite();
                return null;
            }

            var page = pageIndex.GetPageIndexItem(pageId);
            return page;
        }


        private static void IndexSite(int languageId) {
            var pageIndex = PageIndex.CreatePageIndex(languageId);

            _pageLanguageIndex.RemoveAll(i => i.LanguageId == languageId);
            _pageLanguageIndex.Add(pageIndex);
        }


        public static event PageEventHandler PageSaved {
            add {
                _pageSaved -= value;
                _pageSaved += value;
            }
            remove {
                _pageSaved -= value;
            }
        }


        public static event PageEventHandler PageDeleted {
            add {
                _pageDeleted -= value;
                _pageDeleted += value;
            }
            remove {
                _pageDeleted -= value;
            }
        }


        public static void MovePage(Guid pageId, Guid targetId) {
            foreach (PageIndex pageIndex in _pageLanguageIndex) {
                pageIndex.MovePage(pageId, targetId);
            }
        }


        internal static string GetUrlForPageInstanceId(int pageInstanceId) {
            foreach (var pageIndex in _pageLanguageIndex) {
                var item = pageIndex.GetPageIndexItem(pageInstanceId);
                if(item!=null) {
                    return item.PageUrl;
                }
            }

            return string.Empty;
        }


        public static void DeletePage(Guid pageId) {
            // TODO: Only remove per language
            var pageIds = PageData.DeletePage(pageId);

            foreach (var pageIndex in _pageLanguageIndex) {
                pageIndex.DeletePages(pageIds);
                SearchManager.Instance.RemoveFromIndex(pageIds, pageIndex.LanguageId);
            }

            RaisePageDeleted(pageId, 0);
        }

    }
}
