using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Management.Automation;
using System.Management.Automation.Provider;
using ShareFile.Api.Client.Exceptions;
using ShareFile.Api.Client.Requests.Filters;


namespace ShareFile.Api.Powershell
{
    [CmdletProvider("ShareFile", ProviderCapabilities.Credentials | ProviderCapabilities.ExpandWildcards)]
    public class ShareFileProvider : NavigationCmdletProvider
    {
        protected override object NewDriveDynamicParameters()
        {
            return new ShareFileDriveParameters();
        }

        protected override PSDriveInfo NewDrive(PSDriveInfo drive)
        {
            var driveParams = this.DynamicParameters as ShareFileDriveParameters;
            return new ShareFileDriveInfo(drive, driveParams);
        }

        protected override void GetItem(string path)
        {
            var di = (ShareFileDriveInfo)this.PSDriveInfo;
            if (path.IndexOf('*') > 0)
            {
                var items = GetShareFileItems(di, path);
                if (items.Count() > 0) WriteItemObject(items.ElementAt(0), path, typeof(ShareFile.Api.Models.Folder).IsAssignableFrom(items.ElementAt(0).GetType()));
            }
            else
            {
                var item = GetShareFileItem(di, path);
                if (item != null) WriteItemObject(item, path, typeof(ShareFile.Api.Models.Folder).IsAssignableFrom(item.GetType()));
            }
        }

        protected override void GetChildItems(string path, bool recurse)
        {
            var di = (ShareFileDriveInfo)this.PSDriveInfo;
            var children = GetShareFileChildren(di, path);
            if (children != null)
            {
                foreach (var child in children.Feed)
                {
                    WriteItemObject(child, System.IO.Path.Combine(path, child.FileName), typeof(ShareFile.Api.Models.Folder).IsAssignableFrom(child.GetType()));
                }
            }
        }

        protected override void GetChildNames(string path, ReturnContainers returnContainers)
        {
            var di = (ShareFileDriveInfo)this.PSDriveInfo;
            var children = GetShareFileChildren(di, path, new string[] { "FileName" });
            if (children != null)
            {
                foreach (var child in children.Feed)
                {
                    if (typeof(ShareFile.Api.Models.Folder).IsAssignableFrom(child.GetType()) && returnContainers == ReturnContainers.ReturnAllContainers)
                    {
                        WriteItemObject(child.FileName, path, true);
                    }
                    else
                    {
                        WriteItemObject(child, System.IO.Path.Combine(path, child.FileName), typeof(ShareFile.Api.Models.Folder).IsAssignableFrom(child.GetType()));
                    }
                }
            }
        }

        protected override bool IsItemContainer(string path)
        {
            var di = (ShareFileDriveInfo)this.PSDriveInfo;
            var item = GetShareFileItem(di, path, new string[] { "Id" });
            return item is Models.Folder || item is Models.SymbolicLink;
        }

        protected override bool HasChildItems(string path)
        {
            var di = (ShareFileDriveInfo)this.PSDriveInfo;
            var item = GetShareFileItem(di, path, new string[] { "Id", "FileCount" });
            return item is Models.Folder && ((Models.Folder)item).FileCount > 0;
        }

        protected override bool ItemExists(string path)
        {
            return true;
        }

        protected override bool IsValidPath(string path)
        {
            return true;
        }

        protected override void CopyItem(string path, string copyPath, bool recurse)
        {
            var di = (ShareFileDriveInfo)this.PSDriveInfo;
            try
            {
                var source = GetShareFileItem(di, path, new string[] { "Id" });
                var target = GetShareFileItem(di, copyPath, new string[] { "Id" });
                di.Client.Items.Copy(source.url.ToString(), target.Id, Force).Select("Id").Execute();
            }
            catch (Exception e)
            {
                WriteError(new ErrorRecord(e, "ShareFile", ErrorCategory.NotSpecified, path));
            }
        }

        protected override void RemoveItem(string path, bool recurse)
        {
            var di = (ShareFileDriveInfo)this.PSDriveInfo;
            try
            {
                var source = GetShareFileItem(di, path, new string[] { "Id", "url" });
                di.Client.Items.Delete(source.url.ToString())
                    .Execute();
            }
            catch (Exception e)
            {
                WriteError(new ErrorRecord(e, "ShareFile", ErrorCategory.NotSpecified, path));
            }
        }

        protected override void RenameItem(string path, string newName)
        {
            var di = (ShareFileDriveInfo)this.PSDriveInfo;
            try
            {
                var source = GetShareFileItem(di, path, new string[] { "Id" });
                Models.Item newItem = new Models.Item();
                newItem.Name = newName;
                newItem.FileName = newName;
                di.Client.Items.Update(source.url.ToString(), newItem).Execute();
            }
            catch (Exception e)
            {
                WriteError(new ErrorRecord(e, "ShareFile", ErrorCategory.NotSpecified, path));
            }
        }

        protected override object NewItemDynamicParameters(string path, string itemTypeName, object newItemValue)
        {
            return new NewItemParameters();
        }
        
        protected override void NewItem(string path, string itemTypeName, object newItemValue)
        {
            var di = (ShareFileDriveInfo)this.PSDriveInfo;
            var p = this.DynamicParameters as NewItemParameters;
            var itemName = GetChildName(path);
            var parent = GetShareFileItem(di, GetParentPath(path, PSDriveInfo.Root), new string[] { "Id", "url" });
            Models.Item newItem = null;
            var isContainer = false;
            if (itemTypeName == null || itemTypeName.ToLower().Equals("folder") || itemTypeName.ToLower().Equals("directory"))
            {
                var folder = new Models.Folder() { Name = itemName, Description = p.Details };
                newItem = di.Client.Items.CreateFolder(parent.url.ToString(), folder, Force.ToBool()).Execute();
                isContainer = true;
            }
            else if (itemTypeName.ToLower().Equals("symboliclink"))
            {
                if (p.Uri != null) 
                {
                    var symlink = new Models.SymbolicLink() { Name = itemName, Link = p.Uri, Description = p.Details };
                    newItem = di.Client.Items.CreateSymbolicLink(parent.url.ToString(), symlink, Force.ToBool()).Execute();
                    isContainer = false;
                }
            }
            if (newItem != null) WriteItemObject(newItem, path, isContainer);
        }

        public static Models.Item GetShareFileItem(ShareFileDriveInfo driveInfo, string path, string[] select = null, string[] expand = null)
        {
            Client.Requests.IQuery<Models.Item> query = null;
            if (!string.IsNullOrEmpty(path))
            {
                // this regex matches all supported powershell path syntaxes:
                //  drive-qualified - users:/username
                //  provider-qualified - membership::users:/username
                //  provider-internal - users:/username
                var match = Regex.Match(path, @"(?:membership::)?(?:\w+:[\\/])?(?<path>.+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string sfPath = match.Groups["path"].Value;
                    sfPath = sfPath.Replace('\\', '/');
                    if (!sfPath.StartsWith("/")) sfPath = "/" + sfPath;
                    query = driveInfo.Client.Items.ByPath(driveInfo.RootId ?? "", sfPath);
                }
            }
            else
            {
                query = driveInfo.RootId != null ? driveInfo.Client.Items.Get(driveInfo.RootId) : driveInfo.Client.Items.ByPath("/");
            }
            if (query != null)
            {
                return ExecuteQuery<Models.Item>(query, select, expand);
            }
            else return null;
        }

        public static IEnumerable<Models.Item> GetShareFileItems(ShareFileDriveInfo driveInfo, string path, string[] select = null, string[] expand = null)
        {
            Client.Requests.IQuery<Models.ODataFeed<Models.Item>> query = null;
            if (!string.IsNullOrEmpty(path))
            {
                // this regex matches all supported powershell path syntaxes:
                //  drive-qualified - users:/username
                //  provider-qualified - membership::users:/username
                //  provider-internal - users:/username
                var match = Regex.Match(path, @"(?:membership::)?(?:\w+:[\\/])?(?<path>.+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string sfPath = match.Groups["path"].Value;
                    sfPath = sfPath.Replace('\\', '/');
                    if (!sfPath.StartsWith("/")) sfPath = "/" + sfPath;
                    if (sfPath.IndexOf('*') > 0)
                    {
                        var starIndex = sfPath.LastIndexOf('*');
                        var parentIdx = sfPath.LastIndexOf('/');
                        if (parentIdx >= 0)
                        {
                            var sfPathParent = parentIdx > 0 ? sfPath.Substring(0, parentIdx) : "/";
                            var sfPathFile = sfPath.Substring(parentIdx + 1, starIndex - parentIdx - 1);
                            var sfItem = driveInfo.Client.Items.ByPath(driveInfo.RootId ?? "", sfPathParent).Select("Id").Execute();
                            var filter = new StartsWithFilter("Name", sfPathFile, true);
                            query = driveInfo.Client.Items.GetChildren(sfItem.Id).Filter(filter);
                        }
                    }
                }
            }
            if (query != null)
            {
                var feed = ExecuteQuery<Models.ODataFeed<Models.Item>>(query, select, expand);
                return feed.Feed;
            }
            else return null;
        }

        public static Models.ODataFeed<Models.Item> GetShareFileChildren(ShareFileDriveInfo driveInfo, string path, string[] select = null, string[] expand = null)
        {
            var item = GetShareFileItem(driveInfo, path, new string[] { "Id" });
            if (item != null && item is Models.Folder)
            {
                var query = item.url != null ? driveInfo.Client.Items.GetChildren(item.url.ToString()) : driveInfo.Client.Items.GetChildren(item.Id);
                return ExecuteQuery<Models.ODataFeed<Models.Item>>(query, select, expand);
            }
            return null;
        }

        private static T ExecuteQuery<T>(Client.Requests.IQuery<T> query, string[] select = null, string[] expand = null)
            where T : Models.ODataObject
        {
            if (select != null) foreach (var s in select) query.Select(s);
            if (expand != null) foreach (var e in expand)
                {
                    query.Expand(e);
                    if (select != null) query.Select(e);
                }
            try
            {
                return query.Execute();
            }
            catch (ODataException e)
            {
                if (e.Code != System.Net.HttpStatusCode.NotFound) throw e;
            }
            return default(T);
        }
    }
}  
