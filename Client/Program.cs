﻿/*
 * Erstellt mit SharpDevelop.
 * Benutzer: christoph
 * Datum: 28.09.2013
 * Zeit: 12:18
 * 
 * Sie können diese Vorlage unter Extras > Optionen > Codeerstellung > Standardheader ändern.
 */
using System;
using Client;
using System.Collections.Generic;
using System.IO;
  using System.Threading;
namespace Client
{
	class Program
	{
		static Client.ClientClass client;
		static string url = "";
		static string syncPath = "";
		
		public static void Main(string[] args)
		{			
			url = "https://redundancy.pfweb.eu/Demo/Includes/API/api.inc.php";
			syncPath = Environment.CurrentDirectory + @"\Sync\";
			Console.WriteLine("Redundancy 2 API Deamon");			
			Console.WriteLine("Deamon Version: " + System.Reflection.Assembly.GetCallingAssembly().GetName().Version);
			Console.WriteLine("Server: " + new Uri(url).Host);
			Console.WriteLine("Syncronizing into : " + syncPath);	
			
			
			init();
			TimerCallback callback = new TimerCallback(Tick);          
         	 // create a one second timer tick
          	Timer stateTimer = new Timer(callback, null, 0, 5000);
			Console.Read();
		}
		static public void Tick(Object stateInfo)
		{
			
			if (client != null && client.isBusy == false){
				Console.WriteLine("Starting planned refresh");
				client.getFiles("/",true);
			}
		}
		static void init(){
			client = new Client.ClientClass("64c8e7bbd16a75df4d6d11cad83aaeb5fd96df76add7524ccf73d6fafae0d55ff7b1ae11883231343000b07a8bc5a63e55540ad7e8446a947808c1198d2c07d4",url); //apitestuser
			Console.WriteLine("Server API Version: " + client.getVersion());
			bool ack = client.acknoledge();
			Console.WriteLine("ACK: " + ack );
			if (ack)
				Console.WriteLine("Deamon is ready");
			else
			{
				Console.WriteLine("Deamon not ready");
				return;
			}
			//List<Entry> Files = new List<Entry>();		
			FileSystemWatcher fsw = new FileSystemWatcher(syncPath);
			FileSystemWatcher fsw_files = new FileSystemWatcher(syncPath);
			fsw_files.NotifyFilter = NotifyFilters.LastWrite;
			fsw.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.LastAccess ;
			fsw.Created += new FileSystemEventHandler(fsw_Created);
			fsw.Deleted += new FileSystemEventHandler(fsw_Deleted);
			fsw_files.Changed += new FileSystemEventHandler(fsw_Changed);
			fsw.Renamed += new RenamedEventHandler(fsw_Renamed);
			fsw_files.IncludeSubdirectories = true;
			fsw_files.EnableRaisingEvents = true;
			fsw.IncludeSubdirectories = true;
			fsw.EnableRaisingEvents = true;	
			
		}
		static void fsw_Renamed(object sender, RenamedEventArgs e)
		{
			bool isDir = Directory.Exists( e.FullPath);
			string path = e.FullPath.Replace(syncPath,"/");
			string Name = "";
			string OldName = "";
			path = path.Replace(@"\",@"/");
			string root = "/";
			if (isDir){
					DirectoryInfo dI = new DirectoryInfo(e.FullPath);
					root = dI.Parent.FullName.Replace(syncPath,"").Replace("\\","/");
					if (root != "/")
						root += "/";
					Name = dI.Name;	
					OldName = new DirectoryInfo(e.OldFullPath).Name;
					string source = root + OldName + "/" ;
					string old_root = root;
					string newname = Name;
					Console.WriteLine("Action: Folder renamed!");
					Console.WriteLine("Directory: {0} | Name: {1} | Root: {2}",source,Name,old_root);
					if (client.renameFolder(source,newname,old_root,old_root)){
						Console.WriteLine("Action: Success!");
					}
					else{
						Console.WriteLine("Action: Failed!");						
					}
			}
			else
			{
				FileInfo fI = new FileInfo(e.FullPath);
				root = "/" +  (fI.Directory.FullName + "\\").Replace(syncPath,"").Replace("\\","/");
				if (root != "/")
					root += "/";
				Name = fI.Name;
				OldName = new FileInfo(e.OldFullPath).Name;
				Console.WriteLine(OldName);
				string source = root + OldName;
				string old_root = root;
				string newname = Name;				
				Console.WriteLine("Action: File renamed!");
				Console.WriteLine("File: {0} | Name: {1} | Root: {2}",source,Name,old_root);
				if (client.renameFile(client.getHash(OldName,root),newname,old_root)){
					Console.WriteLine("Action: Success!");
				}
				else{
					Console.WriteLine("Action: Failed!");						
				}
			}
			
			
		}

		static void fsw_Changed(object sender, FileSystemEventArgs e)
		{		
			string path = e.FullPath.Replace(syncPath,"/");
			path = path.Replace(@"\",@"/");
			FileInfo n = new FileInfo(e.FullPath);
			bool isDir = false;
			if (n.Extension == "")
			{
			   isDir = true;
			}	
			DirectoryInfo dI = new DirectoryInfo(e.FullPath);	
			
			if (!isDir)			
			{
				if (client.exists(n.Name,path.Replace(n.Name,"")) == true){
					Console.WriteLine("Action: File modified!");
					Console.WriteLine("Directory: {0} | Name: {1}", path.Replace(n.Name,""),n.Name);
					if (client.deleteFile(client.getHash(n.Name,path.Replace(n.Name,"")))){
						Console.WriteLine("Action: Delete old file: Success!");
					}
					else{
						Console.WriteLine("Action: Delete old file: Failed!");
					}
					if (client.uploadFile(e.FullPath,path.Replace(n.Name,""))){
						Console.WriteLine("Action: Success!");
					}
					else{
						Console.WriteLine("Action: Failed!");
					}
				}
			}
		}

		static void fsw_Deleted(object sender, FileSystemEventArgs e)
		{			
		
			bool isDir = false;
			string filename = Path.GetExtension(e.FullPath);
			FileInfo n = new FileInfo(e.FullPath);
			if (n.Extension == "")
			{
			   isDir = true;
			}
			string path = e.FullPath.Replace(syncPath,"/");
			string Name = "";
			path = path.Replace(@"\",@"/");
			string root = "/";
			if (isDir){
					DirectoryInfo dI = new DirectoryInfo(e.FullPath);
					root = "/" + (dI.Parent.FullName  + "\\").Replace(syncPath,"").Replace("\\","/");
					if (root != "/")
						root += "/";
					Name = dI.Name;				
					root = root + Name + "/";
					root = root.Replace("//","/");
					Console.WriteLine("Action: Folder deleted!");
					Console.WriteLine("Directory: {0} | Name: {1}",root,Name);
					if (client.deleteFolder(root))
						Console.WriteLine("Action: Success!");
					else
						Console.WriteLine("Action: Failed!");
			}
			else
			{
				
				FileInfo fI = new FileInfo(e.FullPath);
				root = (fI.Directory.FullName + "\\").Replace(syncPath,"").Replace("\\","/");
				if (root != "/")
					root += "/";
				Name = fI.Name;		
				Console.WriteLine("Action: File deleted!");
				Console.WriteLine("Directory: {0} | Name: {1}",root,Name);
				if (client.deleteFile(client.getHash(Name,root)))
					Console.WriteLine("Action: Success!");
				else
					Console.WriteLine("Action: Failed!");
			}
		}

		static void fsw_Created(object sender, FileSystemEventArgs e)
		{			
			string path = e.FullPath.Replace(syncPath,"/");
			path = path.Replace(@"\",@"/");
			FileInfo n = new FileInfo(e.FullPath);
			bool isDir = false;
			if (n.Extension == "")
			{
			   isDir = true;
			}	
			DirectoryInfo dI = new DirectoryInfo(e.FullPath);					
			if (isDir)
			{
				Console.WriteLine("Action: Folder created!");
				Console.WriteLine("Directory: {0} | Name: {1}",path.Replace(n.Name,""),n.Name);				
				if (client.createDir(n.Name,path.Replace(n.Name,""))){
					Console.WriteLine("Action: success!");
				}
				else{
					Console.WriteLine("Action: Failed!");
				}
				foreach (string file in System.IO.Directory.GetFiles(e.FullPath)) {
					string dirName = new DirectoryInfo(file).Name;
					  var eventArgs = new FileSystemEventArgs(
                    WatcherChangeTypes.Created,
                    Path.GetDirectoryName(file),
                    Path.GetFileName(file));
              	  fsw_Created(sender, eventArgs);				
				}
				foreach (string dir in System.IO.Directory.GetDirectories(e.FullPath)) {
					string dirName = new DirectoryInfo(dir).Name;
					  var eventArgs = new FileSystemEventArgs(
                    WatcherChangeTypes.Created,
                    Path.GetDirectoryName(dir),
                    Path.GetFileName(dir));
              	  fsw_Created(sender, eventArgs);				
				}
			}
			else
			{
				Console.WriteLine("Action: File created!");
				Console.WriteLine("Directory: {0} | Name: {1}",path.Replace(n.Name,""),new FileInfo(e.FullPath).Name);
				if (client.uploadFile(e.FullPath,path.Replace(n.Name,""))){
					Console.WriteLine("Action: success!");
				}
				else{
					Console.WriteLine("Action: Failed!");
				}
				
			}
		}
		
	}
}