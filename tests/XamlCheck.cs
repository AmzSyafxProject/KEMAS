// Validates XAML against the real WPF assemblies: element types, properties/events, attached properties, resource keys.
using System; using System.Linq; using System.Reflection; using System.Xml; using System.Collections.Generic;
class X {
  static Assembly[] asms; static int errors;
  static string[] ns = { "System.Windows","System.Windows.Controls","System.Windows.Controls.Primitives","System.Windows.Shapes","System.Windows.Media",
    "System.Windows.Media.Animation","System.Windows.Documents","System.Windows.Media.Effects","System.Windows.Input","System.Windows.Shell","System.Windows.Data" };
  static Type Find(string name){ foreach(var a in asms) foreach(var n in ns){ var t=a.GetType(n+"."+name); if(t!=null) return t; } return null; }
  static bool HasMember(Type t,string m){
    for(var c=t;c!=null;c=c.BaseType){
      if(c.GetProperty(m,BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)!=null) return true;
      if(c.GetEvent(m,BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)!=null) return true;
      if(c.GetField(m+"Event",BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly)!=null) return true;
    } return false; }
  static bool Attached(string owner,string prop){ var t=Find(owner); if(t==null) return false;
    for(var c=t;c!=null;c=c.BaseType){ if(c.GetMethod("Set"+prop,BindingFlags.Public|BindingFlags.Static)!=null||c.GetField(prop+"Property",BindingFlags.Public|BindingFlags.Static)!=null||c.GetField(prop+"Event",BindingFlags.Public|BindingFlags.Static)!=null) return true; } return false; }
  static void Err(string s){ Console.WriteLine("ERROR "+s); errors++; }
  static HashSet<string> keys=new HashSet<string>();
  static void Walk(XmlElement e, Type parentType){
    string local=e.LocalName;
    if(local.Contains(".")){ // property element  Owner.Prop
      var parts=local.Split('.'); var ot=Find(parts[0]);
      if(ot==null) Err("unknown owner "+local); else if(!HasMember(ot,parts[1]) && !Attached(parts[0],parts[1])) Err("no property "+local);
      foreach(XmlNode c in e.ChildNodes) if(c is XmlElement) Walk((XmlElement)c, ot);
      return; }
    if(e.NamespaceURI.Contains("xaml\"")||e.Prefix=="x"){ return; }
    var t=Find(local); if(t==null){ Err("unknown element <"+local+">"); return; }
    foreach(XmlAttribute a in e.Attributes){
      if(a.Prefix=="xmlns"||a.Name=="xmlns") continue;
      if(a.Prefix=="x"){ if(a.LocalName=="Key") keys.Add(a.Value); continue; }
      string n=a.LocalName;
      if(n.Contains(".")){ var p=n.Split('.'); if(!Attached(p[0],p[1])) Err("bad attached "+n+" on "+local); }
      else if(!HasMember(t,n)) Err(local+" has no property '"+n+"'");
      foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(a.Value,@"\{StaticResource (\w+)\}"))
        if(!keys.Contains(m.Groups[1].Value)) Err("StaticResource '"+m.Groups[1].Value+"' used before definition ("+local+"."+n+")");
      if(a.Value.StartsWith("{Binding") && a.Value.Contains("Delay=")){ if(Find("Binding")==null||Find("Binding").GetProperty("Delay")==null) Err("Binding.Delay unsupported"); }
    }
    foreach(XmlNode c in e.ChildNodes) if(c is XmlElement) Walk((XmlElement)c, t);
  }
  static void Main(string[] args){
    asms=new[]{"PresentationFramework","PresentationCore","WindowsBase","System.Xaml"}.Select(n=>Assembly.LoadFrom("../ref/"+n+".dll")).ToArray();
    foreach(var f in args){ errors=0; keys.Clear(); var d=new XmlDocument(); d.Load(f); Walk(d.DocumentElement,null); Console.WriteLine(f+": "+(errors==0?"OK":errors+" error(s)")); }
  }
}
