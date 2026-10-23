using System; using System.IO; using System.Linq; using System.Collections.Generic; using Kemas;
class T {
  static List<string> CleanTargetsDefaults(){ return Cleaner.CreateTargets().Where(x=>x.Default).Select(x=>x.Key).ToList(); }
  static int fails=0;
  static void Check(bool ok, string what){ Console.WriteLine((ok?"PASS ":"FAIL ")+what); if(!ok) fails++; }
  static void Main(){
    string root=Path.Combine(Path.GetTempPath(),"kt"); if(Directory.Exists(root)) Directory.Delete(root,true);
    // --- custom rules
    var rules=new List<OrgRule>{ new OrgRule("Gambar Saya", ".JPG, png;*.webp"), new OrgRule("Projek/Edit", "prproj aep"), new OrgRule("", "zip") };
    var map=Organizer.BuildMap(rules);
    Check(map[".jpg"]=="Gambar Saya" && map[".PNG"]=="Gambar Saya" && map[".webp"]=="Gambar Saya", "custom rule names + mixed separators");
    Check(map[".prproj"]=="Projek-Edit", "invalid folder chars sanitized: "+map[".prproj"]);
    Check(!map.ContainsKey(".zip"), "empty rule name ignored");
    // --- plan / run / undo
    var dl=Path.Combine(root,"Downloads"); Directory.CreateDirectory(dl);
    foreach(var n in new[]{"a.jpg","b.PNG","c.prproj","d.zip","e.crdownload","f.lnk","g"}) { var p=Path.Combine(dl,n); File.WriteAllText(p,"x"); File.SetLastWriteTime(p,new DateTime(2026,3,5)); }
    File.WriteAllText(Path.Combine(dl,"new.jpg"),"fresh");
    var folders=new List<OrgFolder>{ new OrgFolder("Downloads",dl,true,false) };
    var plan=Organizer.Plan(folders,rules,"Lain-lain",0);
    Check(plan.Count==5, "plan skips partial downloads, shortcuts, brand-new files ("+plan.Count+")");
    Check(plan.Any(p=>p.File=="d.zip" && p.Destination.EndsWith("Lain-lain")), "unknown types go to custom 'others' name");
    Directory.CreateDirectory(Path.Combine(dl,"Gambar Saya")); File.WriteAllText(Path.Combine(dl,"Gambar Saya","a.jpg"),"old");
    File.Delete(Path.Combine(dl,"c.prproj"));
    AppInfoOverride();
    var res=Organizer.Run(plan,()=>false,null);
    Check(res.Moved==4 && res.Skipped.Count==1 && res.Skipped[0].StartsWith("c.prproj|"), "run: moved 4, missing file reported ("+string.Join(",",res.Skipped)+")");
    Check(File.Exists(Path.Combine(dl,"Gambar Saya","a (1).jpg")), "name clash -> 'a (1).jpg'");
    var und=Organizer.Undo(Organizer.LastUndoFile(),null);
    Check(und.Moved==4 && File.Exists(Path.Combine(dl,"a.jpg")) && File.ReadAllText(Path.Combine(dl,"Gambar Saya","a.jpg"))=="old", "undo restores files, keeps pre-existing file");
    Check(!Directory.Exists(Path.Combine(dl,"Lain-lain")), "undo removes folders it emptied");
    // --- date folders
    var dplan=Organizer.Plan(new List<OrgFolder>{ new OrgFolder("D",dl,true,true) },rules,"x",1);
    Check(dplan.All(p=>p.Destination.EndsWith("2026-03")), "date format 2026-03");
    // --- cleaner
    var junk=Path.Combine(root,"junk"); Directory.CreateDirectory(Path.Combine(junk,"sub","deep"));
    File.WriteAllText(Path.Combine(junk,"old.tmp"),new string('x',1000)); File.SetLastWriteTime(Path.Combine(junk,"old.tmp"),DateTime.Now.AddDays(-3));
    File.WriteAllText(Path.Combine(junk,"recent.tmp"),new string('x',500));
    File.WriteAllText(Path.Combine(junk,"sub","deep","z.log"),new string('y',2000)); File.SetLastWriteTime(Path.Combine(junk,"sub","deep","z.log"),DateTime.Now.AddDays(-3));
    var c=new Cleaner{SkipRecentTemp=true};
    Check(c.TreeSize(junk)==3500, "tree size");
    c.ClearFolder(junk,true);
    Check(File.Exists(Path.Combine(junk,"recent.tmp")) && !File.Exists(Path.Combine(junk,"old.tmp")) && !Directory.Exists(Path.Combine(junk,"sub")), "temp clean keeps <24h files, removes old + empty dirs");
    var t=new CleanTarget{Key="x",Folders=()=>new[]{junk},Custom=()=>{throw new CleanException("err_recycle");}};
    var r=c.Clean(t);
    Check(r.State==ItemState.Partial && r.Notes.Contains("err_recycle") && r.Freed==500, "failed step -> partial, rest still cleaned ("+r.State+","+r.Freed+")");
    { var th=Path.Combine(junk,"Microsoft","Windows","Themes"); Directory.CreateDirectory(th); File.WriteAllText(Path.Combine(th,"TranscodedWallpaper"),"w"); File.SetLastWriteTime(Path.Combine(th,"TranscodedWallpaper"),DateTime.Now.AddDays(-9));
      File.WriteAllText(Path.Combine(junk,"old2.tmp"),"x"); File.SetLastWriteTime(Path.Combine(junk,"old2.tmp"),DateTime.Now.AddDays(-9));
      c.ClearFolder(junk,true);
      Check(File.Exists(Path.Combine(th,"TranscodedWallpaper")) && !File.Exists(Path.Combine(junk,"old2.tmp")), "wallpaper cache survives, other old temp removed"); }
    { var g=new CleanTarget{Key="g",Folders=()=>new[]{junk},Guard=()=>"note_wu_pending"}; var gr=c.Clean(g); Check(gr.State==ItemState.Skipped && gr.Notes.Contains("note_wu_pending") && gr.Freed==0, "guard skips item"); }
    { var d=CleanTargetsDefaults(); Check(!d.Contains("prefetch") && !d.Contains("recycle") && d.Contains("usertemp"), "prefetch+recycle off by default"); Check(!new Settings().TrimRamOnSmart, "RAM trim off by default"); }
    Check(Memory.IsShell("explorer") && Memory.IsShell("Explorer.exe") && Memory.IsShell("dwm") && !Memory.IsShell("notepad") && Memory.Kill("explorer")==0, "explorer is never trimmed or ended");
    Check(!c.ClearFolder("/",false) && !c.ClearFolder(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),false), "refuses to clear protected roots");
    // --- update tags
    Check(Updater.ParseTag("v3.1.0")==new Version(3,1,0) && Updater.ParseTag("3.2")==new Version(3,2) && Updater.ParseTag("v4")==new Version(4,0) && Updater.ParseTag("abc")==null, "release tag parsing");
    Check(Updater.ParseTag("v3.4.3")>AppInfo.ParsedVersion && !(Updater.ParseTag("v3.4.2")>AppInfo.ParsedVersion) && !(Updater.ParseTag("v3.2.0")>AppInfo.ParsedVersion), "version compare");
    { var b=Support.Body("Problem report","It crashed",true,300); Check(b.Contains("It crashed") && b.Contains("KEMAS: 3.4.2") && Support.MailtoUrl("Question","hi",true).StartsWith("mailto:amzarsyaf2002@gmail.com?subject=") && Support.MailtoUrl("Question",new string('x',5000),true).Length < 2100 && Support.GmailUrl("Suggestion","hi",false).Contains("su=KEMAS"), "support report builder"); }
    { var st=Json.Read<Settings>("{\"Theme\":\"light\"}"); Check(st.RemoveLockedAtRestart && !st.WelcomeDone, "new settings defaults"); }
    { var ts=Cleaner.CreateTargets(); Check(ts.Where(Cleaner.IsDeep).All(x=>!x.Default) && ts.Count(Cleaner.IsDeep)==6 && ts.Select(x=>x.Key).Distinct().Count()==ts.Count, "deep targets off by default, unique keys"); }
    // --- settings + export roundtrip
    var s=new Settings(); s.FillDefaults(); s.OrgRules[0].Name="Foto Kahwin"; 
    var back=Json.Read<Settings>(Json.Write(s)); back.FillDefaults();
    Check(back.OrgRules[0].Name=="Foto Kahwin" && back.OrgFolders.Count==5 && back.Theme=="dark", "settings json roundtrip");
    var prof=Json.Read<OrgProfile>(Json.Write(new OrgProfile{Rules=s.OrgRules,OthersName="Lain",DateFormat=2}));
    Check(prof.Rules.Count==s.OrgRules.Count && prof.OthersName=="Lain" && prof.DateFormat==2, "export/import profile roundtrip");
    var old=Json.Read<Settings>("{\"Theme\":\"light\"}"); old.FillDefaults();
    Check(old.Theme=="light" && old.OrgRules.Count>0 && old.Language=="en" && old.AutoClean=="off", "old/partial settings files get defaults ("+old.Language+","+old.AutoClean+")");
    Console.WriteLine(fails==0?"ALL PASSED":fails+" FAILED");
  }
  static void AppInfoOverride(){}
}
