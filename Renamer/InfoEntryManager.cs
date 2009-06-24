﻿using System;
using System.Collections.Generic;
using System.Text;
using Renamer.Classes;
using System.IO;
using System.Collections;
using Renamer.Classes.Configuration.Keywords;
using System.Text.RegularExpressions;
using Renamer.Dialogs;
using System.Windows.Forms;
using Renamer.Logging;

namespace Renamer
{
    class InfoEntryManager : IEnumerable
    {
        protected static InfoEntryManager instance;
        private static object m_lock = new object();

        public static InfoEntryManager Instance {
            get {
                if (instance == null) {
                    lock (m_lock) { if (instance == null) instance = new InfoEntryManager(); }
                }
                return instance;
            }
        }


        /// <summary>
        /// List of files, their target destinations etc
        /// </summary>
        protected List<InfoEntry> episodes = new List<InfoEntry>();

        public void Clear() {
            this.episodes.Clear();
        }

        public InfoEntry this[int index] {
            get {
                return this.episodes[index];
            }
        }

        public int Count {
            get {
                return this.episodes.Count;
            }
        }

        public void Remove(InfoEntry ie) {
            this.episodes.Remove(ie);
        }
        public int IndexOf(InfoEntry ie)
        {
            for (int i = 0; i < episodes.Count; i++)
            {
                if (episodes[i] == ie)
                {
                    return i;
                }
            }
            return -1;
        }
        public void RemoveMissingFileEntries() {
            //scan for files which got deleted so we can remove them
            InfoEntry ie;
            for (int i = 0; i < this.episodes.Count; i++) {
                ie = this.episodes[i];
                if (!File.Exists(ie.Filepath + Path.DirectorySeparatorChar + ie.Name)) {
                    this.episodes.Remove(ie);
                    i--;
                }
            }
        }

        /// <summary>
        /// creates names for all entries using season, episode and name and the target pattern
        /// <param name="movie">If used on movie files, target pattern will be ignored and only name property is used</param>
        /// </summary>
        public void CreateNewNames() {
            for (int i = 0; i < this.episodes.Count; i++) {
                this.episodes[i].CreateNewName();
            }
        }
        public InfoEntry GetByListViewItem(ListViewItem lvi)
        {
            return episodes[(int)lvi.Tag];
        }

        /// <summary>
        /// Gets video files matching season and episode number
        /// </summary>
        /// <param name="season">season to search for</param>
        /// <param name="episode">episode to search for</param>
        /// <returns>List of all matching InfoEntries, never null, but may be empty</returns>
        public List<InfoEntry> GetMatchingVideos(int season, int episode) {
            List<InfoEntry> lie = new List<InfoEntry>();
            foreach (InfoEntry ie in this.episodes) {
                if (ie.Season == season && ie.Episode == episode && ie.IsVideofile) {
                    lie.Add(ie);
                }
            }
            return lie;
        }

        /// <summary>
        /// Gets video files matching season and episode number
        /// </summary>
        /// <param name="season">season to search for</param>
        /// <param name="episode">episode to search for</param>
        /// <returns>List of all matching InfoEntries, never null, but may be empty</returns>
        public List<InfoEntry> GetVideos(string showname) {
            List<InfoEntry> lie = new List<InfoEntry>();
            foreach (InfoEntry ie in this.episodes) {
                if (ie.Showname == showname) {
                    lie.Add(ie);
                }
            }
            return lie;
        }

        /// <summary>
        /// Gets subtitle files matching season and episode number
        /// </summary>
        /// <param name="season">season to search for</param>
        /// <param name="episode">episode to search for</param>
        /// <returns>List of all matching InfoEntries, never null, but may be empty</returns>
        public List<InfoEntry> GetMatchingSubtitles(int season, int episode) {
            List<InfoEntry> lie = new List<InfoEntry>();
            foreach (InfoEntry ie in this.episodes) {
                if (ie.Season == season && ie.Episode == episode && ie.IsSubtitle) {
                    lie.Add(ie);
                }
            }
            return lie;
        }

        /// <summary>
        /// Gets video file matching to subtitle
        /// </summary>
        /// <param name="ieSubtitle">InfoEntry of a subtitle to find matching video file for</param>
        /// <returns>Matching video file</returns>
        public InfoEntry GetVideo(InfoEntry ieSubtitle) {
            List<string> vidext = new List<string>(Helper.ReadProperties(Config.Extensions));
            foreach (InfoEntry ie in this.episodes) {
                if (Path.GetFileNameWithoutExtension(ieSubtitle.Filename) == Path.GetFileNameWithoutExtension(ie.Filename)) {
                    if (vidext.Contains(ie.Extension)) {
                        return ie;
                    }
                }
            }
            return null;
        }



        /// <summary>
        /// Gets subtitle file matching to video
        /// </summary>
        /// <param name="ieVideo">InfoEntry of a video to find matching subtitle file for</param>
        /// <returns>Matching subtitle file</returns>
        public InfoEntry GetSubtitle(InfoEntry ieVideo) {
            List<string> subext = new List<string>(Helper.ReadProperties(Config.SubtitleExtensions));
            foreach (InfoEntry ie in this.episodes) {
                if (Path.GetFileNameWithoutExtension(ieVideo.Filename) == Path.GetFileNameWithoutExtension(ie.Filename)) {
                    if (subext.Contains(ie.Extension)) {
                        return ie;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get a video file matching season and episode number
        /// 
        /// </summary>
        /// <param name="season">season to search for</param>
        /// <param name="episode">episode to search for</param>
        /// <returns>InfoEntry of matching video file, null if not found or more than one found</returns>
        public InfoEntry GetMatchingVideo(int season, int episode) {
            List<InfoEntry> lie = GetMatchingVideos(season, episode);
            InfoEntry ie = (lie.Count == 1) ? lie[0] : null;
            return ie;
        }

        #region IEnumerable Member

        public IEnumerator GetEnumerator() {
            return this.episodes.GetEnumerator();
        }

        #endregion

        internal void Add(InfoEntry ie) {
            this.episodes.Add(ie);
        }

        /// <summary>
        /// Creates subtitle destination and names subs when no show information is fetched yet, so they have the same name as their video files for better playback
        /// </summary>
        void RenameSubsToMatchVideos() {
            foreach (InfoEntry ie in this.episodes) {
                if (!ie.IsSubtitle || ie.NewFileName != "") {
                    continue;
                }
                InfoEntry videoEntry = GetMatchingVideo(ie.Season, ie.Episode);
                if (videoEntry == null) {
                    continue;
                }
                if (videoEntry.NewFileName == "") {
                    ie.NewFileName = Path.GetFileNameWithoutExtension(videoEntry.Filename);
                }
                else {
                    ie.NewFileName = Path.GetFileNameWithoutExtension(videoEntry.NewFileName);
                }
                ie.NewFileName += "." + ie.Extension;

                //Move to Video file
                ie.Destination = videoEntry.Destination;

                //Don't do this again if name fits already
                if (ie.NewFileName == ie.Filename) {
                    ie.NewFileName = "";
                }
            }
        }
        /// <summary>
        /// Main Rename function
        /// </summary>
        public void Rename() {
            Helper.InvalidFilenameAction invalidAction = Helper.ReadEnum<Helper.InvalidFilenameAction>(Config.InvalidFilenameAction);
            string replace = Helper.ReadProperty(Config.InvalidCharReplace);

            //Go through all files and do stuff
            for (int i = 0; i < this.episodes.Count; i++) {
                InfoEntry ie = InfoEntryManager.Instance[i];
                if (ie.MarkedForDeletion&&ie.ProcessingRequested)
                {
                    try
                    {
                        File.Delete(ie.Filepath1.Fullfilename);
                        episodes.Remove(ie);
                        //Go back so no entry is skipped after removal of current entry
                        i--;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogMessage("Couldn't delete " + ie.Filepath1.Fullfilename + ": " + ex.Message, LogLevel.ERROR);
                    }
                }
                ie.Rename(ref invalidAction, ref replace);
            }
            if (Helper.ReadBool(Config.DeleteEmptyFolders)) {
                //Delete all empty folders code
                Helper.DeleteAllEmptyFolders(Helper.ReadProperty(Config.LastDirectory), new List<string>(Helper.ReadProperties(Config.IgnoreFiles)));
            }
            //Get a list of all involved folders
            //FillListView();
        }


        /// <summary>
        /// Decides which files should be marked for processing
        /// </summary>
        /// <param name="Basepath">basepath, as always</param>
        /// <param name="Showname">user entered showname</param>
        public void SelectSimilarFilesForProcessing(string Basepath, string Showname) {
            List<InfoEntry> matches = FindSimilarByName(Showname);
            foreach (InfoEntry ie in this.episodes) {
                if (matches.Contains(ie)) {
                    ie.ProcessingRequested = true;
                    ie.Movie = false;
                }
                else {
                    ie.ProcessingRequested = false;
                }
            }
            return;
        }

        /// <summary>
        /// Sets new title to some files and takes care of storing it properly (last [TitleHistorySize] Titles are stored)
        /// </summary>
        /// <param name="files">files to which this title should be set to</param>
        /// <param name="title">name to be set</param>
        public void SetNewTitle(List<InfoEntry> files, string title) {
            string[] LastTitlesOld = Helper.ReadProperties(Config.LastTitles);
            foreach (InfoEntry ie in files) {
                if (ie.Showname != title) {
                    ie.Showname = title;
                }
            }

            //check if list of titles contains new title
            int Index = -1;
            for (int i = 0; i < LastTitlesOld.Length; i++) {
                string str = LastTitlesOld[i];
                if (str == title) {
                    Index = i;
                    break;
                }
            }

            //if the title is new
            if (Index == -1) {
                List<string> LastTitlesNew = new List<string>();
                LastTitlesNew.Add(title);
                foreach (string s in LastTitlesOld) {
                    LastTitlesNew.Add(s);
                }
                int size = Helper.ReadInt(Config.TitleHistorySize);
                Helper.WriteProperties(Config.LastTitles, LastTitlesNew.GetRange(0, Math.Min(LastTitlesNew.Count, size)).ToArray());
            }
            //if the title is in the list already, bring it to the front
            else {
                List<string> items = new List<string>(LastTitlesOld);
                items.RemoveAt(Index);
                items.Insert(0, title);
                Helper.WriteProperties(Config.LastTitles, items.ToArray());
            }
        }
        public void SetPath(string path) {
            string refPath = path;
            SetPath(ref refPath);
        }
        /// <summary>
        /// Sets a new path for the list view
        /// </summary>
        /// <param name="path">Path to be set</param>
        public void SetPath(ref string path) {
            if (path == null || path == "" || !Directory.Exists(path))
                return;

            if (path.Length == 2) {
                if (char.IsLetter(path[0]) && path[1] == ':') {
                    path = path + Path.DirectorySeparatorChar;
                }
            }
            DirectoryInfo currentpath = new DirectoryInfo(path);

            //fix casing of the path if user entered it
            string fixedpath = "";
            while (currentpath.Parent != null) {
                fixedpath = currentpath.Parent.GetDirectories(currentpath.Name)[0].Name + Path.DirectorySeparatorChar + fixedpath;
                currentpath = currentpath.Parent;
            }
            fixedpath = currentpath.Name.ToUpper() + fixedpath;
            fixedpath = fixedpath.TrimEnd(new char[] { Path.DirectorySeparatorChar });
            if (fixedpath.Length == 2) {
                if (char.IsLetter(fixedpath[0]) && fixedpath[1] == ':') {
                    fixedpath = fixedpath + Path.DirectorySeparatorChar;
                }
            }
            path = fixedpath;
            //Same path, ignore
            if (Helper.ReadProperty(Config.LastDirectory) == path) {
                return;
            }
            else {
                Helper.WriteProperty(Config.LastDirectory, path);
                Environment.CurrentDirectory = path;
            }
        }

        /// <summary>
        /// Finds similar files by looking at the filename and comparing it to a showname
        /// </summary>
        /// <param name="Basepath">basepath of the show</param>
        /// <param name="Showname">name of the show to filter</param>
        /// <param name="source">source files</param>
        /// <returns>a list of matches</returns>
        public List<InfoEntry> FindSimilarByName(string Showname) {
            List<InfoEntry> matches = new List<InfoEntry>();
            Showname = Showname.ToLower();
            //whatever, just check path and filename if it contains the showname
            foreach (InfoEntry ie in this.episodes) {
                string[] folders = Helper.splitFilePath(ie.Filepath);
                string processed = ie.Filename.ToLower();

                //try to extract the name from a shortcut, i.e. sga for Stargate Atlantis
                string pattern = "[^\\w]";
                Match m = Regex.Match(processed, pattern, RegexOptions.IgnoreCase);
                if (m != null && m.Success) {
                    string abbreviation = processed.Substring(0, m.Index);
                    if (abbreviation.Length > 0 && Helper.ContainsLetters(abbreviation, Showname)) {
                        matches.Add(ie);
                        continue;
                    }
                }

                //now check if whole showname is in the filename
                string CleanupRegex = Helper.ReadProperty(Config.CleanupRegex);
                processed = Regex.Replace(processed, CleanupRegex, " ");
                if (processed.Contains(Showname)) {
                    matches.Add(ie);
                    continue;
                }

                //or in some top folder
                foreach (string str in folders) {
                    processed = str.ToLower();
                    processed = Regex.Replace(processed, CleanupRegex, " ");
                    if (processed.Contains(Showname)) {
                        matches.Add(ie);
                        break;
                    }
                }
            }
            return matches;
        }


        #region Unsused for now!
        public static int GetNumberOfVideoFilesInFolder(string path) {
            List<string> vidext = new List<string>(Helper.ReadProperties(Config.Extensions));
            int count = 0;
            foreach (string file in Directory.GetFiles(path)) {
                if (vidext.Contains(Path.GetFileNameWithoutExtension(file))) {
                    count++;
                }
            }
            return count;
        }
        #endregion
    }
}
