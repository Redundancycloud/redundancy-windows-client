﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using HttpPostRequestLib.Net;
using System.Diagnostics;
using System.Security;

namespace RedundancyClient
{
    public class Client
    {
        public enum Action
        {
            getFiles,
            getApiKey,
            getFileIDsWithDisplaynames,
            getFileIDsWithDisplaynamesAndFilenamesAndUploadDate,
            getFileHeadsAsXML,
            getPropertiesAsXML,
            getContent,
            getName,
            getVersion,
            getLatestFiles,
            uploadFile,
            renameFile,
            renameFolder,
            copy,
            move,
            getHash,
            exists,
            createDir,
            deleteFile,
            deleteFolder
        }

        public string UserName { get; set; }
        public Uri ApiUri { get; private set; }
        public string UserAgent { get; private set; }
        public int TransactionCount { get; set; }
        public string SyncPath { get; private set; }
        public bool Log { get; set; }

        private string apiKey;
        private SecureString password;

        public Client(string userName, SecureString password, string url, string userAgent, string syncPath)
        {
            this.UserName = userName;
            this.password = password;
            this.ApiUri = new Uri(url);
            this.UserAgent = userAgent;
            this.SyncPath = syncPath;
            this.Log = false;
        }

        private string getApiKey()
        {
            if (Log) Console.Write("Get API Key...");
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("method", Action.getApiKey.ToString());
            request.Post.Add("userName", UserName);
            request.Post.Add("password", StringCryptography.ToInsecureString(password));
            request.UserAgent = UserAgent;
            TransactionCount++;
            string content = parseXMLForSingleValue(request.Submit());
            request.Post.Clear();
            if (Log) Console.WriteLine("done");
            if (content == "")
                return null;
            return content;
        }

        public bool IsReady()
        {
            if (string.IsNullOrEmpty(apiKey))
                apiKey = getApiKey();
            return apiKey != null;
        }

        public string getVersion()
        {
            if (Log) Console.Write("Get API version...");
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("method", Action.getVersion.ToString());
            request.UserAgent = UserAgent;
            TransactionCount++;
            string content = parseXMLForSingleValue(request.Submit());
            if (Log) Console.WriteLine(content);
            return content;
        }

        private string parseXMLForSingleValue(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNode root = doc.DocumentElement;
            return root.InnerText;
        }

        public Dictionary<string, Entry> getFileHeadsAsDic(string dir)
        {
            if (Log) Console.Write("Get file heads of files in {0}...", dir);
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("dir", dir);
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.getFileHeadsAsXML.ToString());
            request.UserAgent = UserAgent;
            TransactionCount++;
            string content = request.Submit();
            Dictionary<string, Entry> entries = ParseXMLForGetFileHeadsAsDic(content);
            if (Log) Console.WriteLine("done");
            return entries;
        }

        Dictionary<string, Entry> ParseXMLForGetFileHeadsAsDic(string xml)
        {
            Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            foreach (XmlNode child in doc.DocumentElement)
            {
                int id = int.Parse(child.Attributes["id"].Value);
                string displayName = child.Attributes["displayName"].Value;
                string fileName = child.Attributes["fileName"].Value;
                DateTime lastWriteTime = DateTime.Parse(child.Attributes["creationTime"].Value);
                Entry entry = new Entry(true, id, displayName, fileName, lastWriteTime);
                entries.Add(displayName, entry);
            }
            return entries;
        }

        /// <summary>
        /// Gibt absoluten Pfad zum im Parameter übergebenen Ordner zurück.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public string getDirPath(string dir)
        {
            if (dir == "/")
                return SyncPath;
            else
                return Path.Combine(SyncPath, preparePath(dir));
        }

        /// <summary>
        /// Verändert Redundancy Ordnerpfade zu Windowspfaden, um Path.Combine(...) verwenden zu können
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string preparePath(string path)
        {
            path = path.Replace('/', '\\');
            return path.Substring(1);
        }

        //private void create(int id)
        //{
        //    Entry entry = getProperties(id);
        //    string path;
        //    if (entry.IsFolder())
        //    {
        //        path = Path.Combine(SyncPath, preparePath(entry.DisplayName));
        //        if (!Directory.Exists(path))
        //        {
        //            if (Log) Console.Write("Create dir {0}...", path);
        //            Directory.CreateDirectory(path);
        //            DirectoryInfo dirInfo = new DirectoryInfo(path);
        //            dirInfo.LastWriteTime = entry.LastWriteTime;
        //            if (Log) Console.WriteLine("done");
        //        }
        //    }
        //    else
        //    {
        //        path = Path.Combine(SyncPath, preparePath(entry.Directory));
        //        path = Path.Combine(path, entry.DisplayName);
        //        if (Log) Console.Write("Get content of {0}...", path);
        //        entry.Content = getContent(id);
        //        if (Log) Console.WriteLine("done");
        //        if (Log) Console.Write("Create file {0}...", path);
        //        File.WriteAllBytes(path, entry.Content);
        //        FileInfo fileInfo = new FileInfo(path);
        //        fileInfo.LastWriteTime = entry.LastWriteTime;
        //        File.SetLastWriteTime(path, entry.LastWriteTime); //TODO: Check
        //        if (Log) Console.WriteLine("done");
        //    }
        //}

        private void create(int id)
        {
            Entry entry = getProperties(id);
            string path;
            if (entry.IsFolder())
            {
                path = Path.Combine(SyncPath, preparePath(entry.DisplayName));
                if (!Directory.Exists(path))
                {
                    if (Log) Console.Write("Create dir {0}...", path);
                    Directory.CreateDirectory(path);
                    DirectoryInfo dirInfo = new DirectoryInfo(path);
                    dirInfo.LastWriteTime = entry.LastWriteTime;
                    if (Log) Console.WriteLine("done");
                }
            }
            else
            {
                path = Path.Combine(SyncPath, preparePath(entry.Directory));
                path = Path.Combine(path, entry.DisplayName);
                if (Log) Console.Write("Get content of {0}...", path);
                entry.Content = getContent(id);
                if (Log) Console.WriteLine("done");
                if (Log) Console.Write("Create file {0}...", path);
                File.WriteAllBytes(path, entry.Content);
                FileInfo fileInfo = new FileInfo(path);
                fileInfo.LastWriteTime = entry.LastWriteTime;
                File.SetLastWriteTime(path, entry.LastWriteTime); //TODO: Check
                if (Log) Console.WriteLine("done");
            }
        }

        private void create(Entry entry)
        {
            string path;
            if (entry.IsFolder())
            {
                path = Path.Combine(SyncPath, preparePath(entry.DisplayName));
                if (!Directory.Exists(path))
                {
                    if (Log) Console.Write("Create dir {0}...", path);
                    Directory.CreateDirectory(path);
                    DirectoryInfo dirInfo = new DirectoryInfo(path);
                    dirInfo.LastWriteTime = entry.LastWriteTime;
                    if (Log) Console.WriteLine("done");
                }
            }
            else
            {
                Entry fileEntry = getProperties(entry.ID);
                path = Path.Combine(SyncPath, preparePath(fileEntry.Directory));
                path = Path.Combine(path, fileEntry.DisplayName);
                if (Log) Console.Write("Get content of {0}...", path);
                fileEntry.Content = getContent(fileEntry.ID);
                if (Log) Console.WriteLine("done");
                if (Log) Console.Write("Create file {0}...", path);
                File.WriteAllBytes(path, fileEntry.Content);
                FileInfo fileInfo = new FileInfo(path);
                fileInfo.LastWriteTime = fileEntry.LastWriteTime;
                File.SetLastWriteTime(path, fileEntry.LastWriteTime); //TODO: Check
                if (Log) Console.WriteLine("done");
            }
        }

        public void create(Dictionary<string, Entry> entries)
        {
            foreach (KeyValuePair<string, Entry> entry in entries)
            {
                if (entry.Value.FromServer)
                {
                    create(entry.Value);
                    if (entry.Value.Entries != null)
                        create(entry.Value.Entries);
                }
                else
                {
                    string path = Path.Combine(getDirPath(entry.Value.Directory), entry.Value.DisplayName);
                    if (entry.Value.IsFolder())
                    {
                        createDir(entry.Value);
                        if (entry.Value.Entries != null)
                            create(entry.Value.Entries);
                    }
                    else
                    {
                        uploadFile(path, entry.Value.Directory, entry.Value.LastWriteTime);
                    }
                }
            }
        }

        public void Sync()
        {
            Sync("/");
        }

        public void Sync(string dir)
        {
            Stopwatch stopwatch = new Stopwatch(); ;
            if (Log)
            {
                stopwatch.Start();
                Console.WriteLine("Start Synchronization of {0}", dir);
            }
            if (string.IsNullOrEmpty(apiKey))
            {
                if (Log) Console.WriteLine("Synchronization cancelled. Authentification failed.");
                return;
            }
            else
                if (Log) Console.WriteLine("Authentification was successfully.");

            if (!Directory.Exists(SyncPath))
                Directory.CreateDirectory(SyncPath);
            create(getNewestFilesNested(dir));
            if (Log)
            {
                stopwatch.Stop();
                Console.WriteLine("Synchronization finished");
                Console.WriteLine("Needed transactions: {0}", TransactionCount);
                Console.WriteLine("Needed time: {0}s", stopwatch.Elapsed.TotalSeconds);
            }
        }

        public Dictionary<string, Entry> getNewestFilesNested(string root)
        {
            Dictionary<string, Entry> localEntries = getLocalFilesNested(root);
            Dictionary<string, Entry> serverEntries = getServerFilesNested(root);
            return getNewestFilesNested(localEntries, serverEntries);
        }

        public Dictionary<string, Entry> getNewestFilesNested(Dictionary<string, Entry> localEntries, Dictionary<string, Entry> serverEntries)
        {
            Dictionary<string, Entry> newestEntries = new Dictionary<string, Entry>();

            foreach (KeyValuePair<string, Entry> entry in localEntries)
            {
                if (serverEntries.ContainsKey(entry.Key)) //falls Eintrag in beiden Listen
                {
                    if (entry.Value.IsFolder()) //falls beides Ordner sind
                    {
                        newestEntries.Add(entry.Key, serverEntries[entry.Key]);
                        newestEntries[entry.Key].Entries = getNewestFilesNested(entry.Value.Entries, serverEntries[entry.Key].Entries);
                    }
                    else //falls beides Dateien sind
                    {
                        if (Log) Console.Write("Compare versions of {0}...", entry.Value.DisplayName);
                        DateTime localLastWriteTime = entry.Value.LastWriteTime;
                        DateTime serverLastWriteTime = serverEntries[entry.Key].LastWriteTime;
                        if (localLastWriteTime != serverLastWriteTime)
                        {
                            if (localLastWriteTime < serverLastWriteTime)
                            {
                                if (Log) Console.WriteLine("server file is newer");
                                newestEntries.Add(entry.Key, serverEntries[entry.Key]);
                            }
                            else
                            {
                                if (Log) Console.WriteLine("client file is newer");
                                newestEntries.Add(entry.Key, entry.Value);
                            }
                        }
                        else
                            if (Log) Console.WriteLine("both up-to-date");
                    }
                    serverEntries.Remove(entry.Key);
                }
                else //falls Eintrag nur in localEntries
                {
                    newestEntries.Add(entry.Key, entry.Value);
                }
            }

            //Einträge müssen nur noch hinz. werden, überschneidende Teilmengen wurden davor gefiltert
            foreach (KeyValuePair<string, Entry> entry in serverEntries)
                newestEntries.Add(entry.Key, entry.Value);

            return newestEntries;
        }

        public Dictionary<string, Entry> getLocalFilesNested(string root)
        {
            Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
            string path = getDirPath(root);
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                entries.Add(fileInfo.Name, new Entry(false)
                {
                    DisplayName = fileInfo.Name,
                    Directory = root,
                    LastWriteTime = fileInfo.LastWriteTime
                });
            }
            string[] directories = Directory.GetDirectories(path);
            foreach (string dir in directories)
            {
                DirectoryInfo dirInfo = new DirectoryInfo(dir);
                string name = root + dirInfo.Name + "/";
                entries.Add(name, new Entry(false)
                {
                    FileName = name,
                    DisplayName = name,
                    Directory = root,
                    LastWriteTime = dirInfo.LastWriteTime,
                    Entries = getLocalFilesNested(name)
                });
            }
            return entries;
        }

        public Dictionary<string, Entry> getServerFilesNested(string root)
        {
            Dictionary<string, Entry> entries = getFileHeadsAsDic(root);
            foreach (KeyValuePair<string, Entry> entry in entries)
                if (entry.Value.IsFolder())
                    entry.Value.Entries = getServerFilesNested(entry.Value.DisplayName);
            return entries;
        }

        public Entry getProperties(int id)
        {
            if (Log) Console.Write("Get properties of {0}...", id);
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("id", id.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.getPropertiesAsXML.ToString());
            request.UserAgent = UserAgent;
            request.Encoding = Encoding.UTF8;
            TransactionCount++;
            string content = request.Submit();

            Entry entry = parseXMLForGetProperties(content);
            if (Log) Console.WriteLine("done");
            return entry;
        }

        private Entry parseXMLForGetProperties(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNode root = doc.DocumentElement;

            int id = int.Parse(root.Attributes["id"].Value);
            string fileName = root.Attributes["fileName"].Value;
            string displayName = root.Attributes["displayName"].Value;
            DateTime lastWriteTime = DateTime.Parse(root.Attributes["creationTime"].Value);
            string hash = root.Attributes["hash"].Value;
            int sizeInByte = int.Parse(root.Attributes["sizeInByte"].Value);
            string userAgent = root.Attributes["userAgent"].Value;
            string directory = root.Attributes["directory"].Value;

            return new Entry(true, id, displayName, fileName, lastWriteTime)
            {
                Hash = hash,
                SizeInByte = sizeInByte,
                UserAgent = userAgent,
                Directory = directory
            };
        }

        Byte[] getContent(int id)
        {
            WebClient client = new WebClient();
            client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            client.Headers.Add(HttpRequestHeader.UserAgent, UserAgent);
            byte[] result = client.UploadData(this.ApiUri.ToString(), "POST",
                System.Text.Encoding.UTF8.GetBytes("id=" + id.ToString() + "&key=" + apiKey + "&method=" + Action.getContent.ToString()));
            TransactionCount++;
            return result;
        }

        public bool uploadFile(string path, string currentDir, DateTime timestamp)
        {
            if (Log) Console.Write("Uploading {0}...", path);
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.uploadFile.ToString());
            request.Post.Add("currentdir", currentDir);
            string time = timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            request.Post.Add("timestamp", timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            request.Files.Add("userfile[]", path);
            request.UserAgent = UserAgent;
            TransactionCount++;
            bool result = bool.Parse(parseXMLForSingleValue(request.Submit()));
            if (Log) Console.WriteLine(result ? "done" : "failed");
            return result;
        }

        public bool renameFile(string hash, string newName, string currentdir)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.renameFile.ToString());
            request.Post.Add("file", hash);
            request.Post.Add("newname", newName);
            request.UserAgent = UserAgent;
            request.Encoding = System.Text.Encoding.UTF8;
            request.Post.Add("currentdir", currentdir);
            string result = request.Submit();
            return result == "false";
        }

        public bool renameFolder(string source, string newName, string old_root, string currentdir)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.renameFolder.ToString());
            request.Post.Add("newname", newName);
            request.Post.Add("source", source);
            request.Post.Add("old_root", old_root);
            request.Post.Add("currentdir", currentdir);
            request.UserAgent = UserAgent;
            string result = request.Submit();
            return result == "false";
        }

        public bool copyFile(string target, string hash)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.copy.ToString());
            request.Post.Add("file", hash);
            request.UserAgent = UserAgent;
            request.Post.Add("dir", target);
            string result = request.Submit();
            return result == "false";
        }

        public bool copyFolder(string source, string target, string old_root)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.copy.ToString());
            request.Post.Add("source", source);
            request.Post.Add("target", target);
            request.Post.Add("old_root", old_root);
            request.UserAgent = UserAgent;
            string result = request.Submit();
            return result == "false";
        }

        public bool moveFile(string target, string hash)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.move.ToString());
            request.Post.Add("file", hash);
            request.UserAgent = UserAgent;
            request.Post.Add("dir", target);
            string result = request.Submit();
            return result == "false";
        }

        public bool moveFolder(string source, string target, string old_root)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.move.ToString());
            request.Post.Add("source", source);
            request.Post.Add("target", target);
            request.Post.Add("old_root", old_root);
            request.UserAgent = UserAgent;
            string result = request.Submit();
            return result == "false";
        }

        public bool exists(string entry, string dir)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.exists.ToString());
            request.Post.Add("entry", entry);
            request.Post.Add("dir", dir);
            request.UserAgent = UserAgent;
            string result = request.Submit();
            return result != "false";
        }

        public bool createDir(Entry entry)
        {
            if (Log) Console.Write("Create directory {0}...", entry.DisplayName);
            string dirName = entry.DisplayName.Substring(entry.Directory.Length);
            dirName = dirName.Substring(0, dirName.Length - 1);
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.createDir.ToString());
            request.Post.Add("entry", dirName);
            request.Post.Add("dir", entry.Directory);
            request.UserAgent = UserAgent;
            bool result = bool.Parse(parseXMLForSingleValue(request.Submit()));
            if (Log) Console.WriteLine(result ? "done" : "failed");
            return result;
        }

        public string getHash(string file, string dir)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.getHash.ToString());
            request.Post.Add("file", file);
            request.Post.Add("dir", dir);
            request.UserAgent = UserAgent;
            //request.Encoding = Encoding.UTF8;
            string result = request.Submit();
            return result;
        }

        public bool deleteFile(string hash)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.deleteFile.ToString());
            request.Post.Add("s", "true");
            request.Post.Add("file", hash);
            request.UserAgent = UserAgent;
            string result = request.Submit();
            return result != "false";
        }

        public bool deleteFolder(string dir)
        {
            HTTPPostRequest request = new HTTPPostRequest(this.ApiUri.ToString());
            request.Post.Add("key", apiKey);
            request.Post.Add("method", Action.deleteFolder.ToString());
            request.Post.Add("s", "true");
            request.Post.Add("dir", dir);
            request.UserAgent = UserAgent;
            string result = request.Submit();
            return result != "false";
        }
    }
}