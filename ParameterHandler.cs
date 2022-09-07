
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#pragma warning disable CS8602, CS8600
namespace Seamlex.Utilities
{
    /// <summary>
    /// Manages parameters
    /// </summary>
    
    public class ParameterHander
    {
        private List<ParameterSetting> ps = new List<ParameterSetting>();
        public string lastmessage = "";
        public string appname = "myappname";

        public string reservedcs = ",abstract,as,base,bool,break,byte,case,catch,char,checked,class,const,continue,decimal,default,delegate,do,double,else,enum,event,explicit,extern,false,finally,fixed,float,for,foreach,goto,if,implicit,in,int,interface,internal,is,lock,long,namespace,new,null,object,operator,out,override,params,private,protected,public,readonly,ref,return,sbyte,sealed,short,sizeof,stackalloc,static,string,struct,switch,this,throw,true,try,typeof,uint,ulong,unchecked,unsafe,ushort,using,virtual,void,volatile,while,";

        public string fieldtypescs = ",guid,byte,int,int32,string,bool,date,datetime,char,decimal,double,float,long,object,sbyte,short,uint,ulong,ushort,";

        public string FixFieldTypeCs(string fieldtypecs)
        {
            string output = "System.String";
            string checktype = (fieldtypecs.ToLower()).Replace("system.","");
            if(!this.fieldtypescs.Contains(','+checktype+','))
                return output;
            if(checktype == "int" || checktype == "int32" )
                return "System.Int32";
            if(checktype == "date" || checktype == "datetime" )
                return "System.DateTime";
            if(checktype == "bool" || checktype == "boolean" )
                return "System.Boolean";
            if(checktype == "decimal" || checktype == "float" || checktype == "double" )
                return "System.Double";
            if(checktype == "byte")
                return "System.Byte";
            if(checktype == "guid")
                return "System.Guid";
            return output;
        }

        public bool SetParameterInfo(List<ParameterSetting> ps)
        {
            this.ps.Clear();
            this.ps.AddRange(ps);
            return true;
        }

        public bool SetParameters(List<string> parameters)
        {
            //this.LoadParameterInfo();

            // we will need to check the first parameter
            // if empty, 
            string category = "";
            string setting = "";
            bool ishelp = false;
            if(parameters.Count==0)
            {
                category = "--help";
                setting = "--help";
                ishelp = true;
            }
            else if( parameters[0].Trim().ToLower() == "--help" || parameters[0].Trim().ToLower() == "-h"  || parameters[0].Trim().ToLower() == "" )
            {
                category = "--help";
                setting = "--help";
                ishelp = true;
            }
            else
            {
                category = parameters[0].Trim().ToLower();
                if(parameters.Count==1)
                {
                    // we validate the category below
                    // setting = "--help";
                    // ishelp = true;
                }
                else if(parameters.Count==2)
                {
                    // if the second parameter is help then do the same as above
                    if(parameters[1].ToLower().Trim().Equals("--help") || parameters[1].ToLower().Trim().Equals("-h"))
                    {
                        setting = "--help";
                        ishelp = true;
                    }
                }
                else
                {
                    // if the final parameter is help then get help on the first parameter after the category (if this is a match)
                    if(parameters[parameters.Count-1].ToLower().Trim().Equals("--help") || parameters[parameters.Count-1].ToLower().Trim().Equals("-h"))
                    {
                        string checkparameter = parameters[1].ToLower().Trim();
                        var checksetting = ps.Find(x => x.category.Equals(category) && (x.setting.Equals(checkparameter) || x.synonym.Equals(checkparameter)));
                        if(checksetting != null)
                        {
                            setting = checksetting.setting;
                            ishelp = true;
                        }
                    }
                }
            }

            if(!ishelp)
            {
                // check if there is a valid category entered
                var checkcategory = ps.Find(x => x.category.Equals(category) && x.setting.Equals("--help"));
                if(checkcategory == null)
                {
                    // this category does not exist
                    // error 
                    this.lastmessage = $"Category '{category}' does not exist.  Enter {this.appname} --help for a list of categories.";
                    return false;
                }

                // if this is just a single category with nothing else, get the help for it
                if(parameters.Count==1)
                {
                    ishelp = true;
                    setting = "--help";
                }
            }

            // if this is a help request, set only the minimal properties and exit
            if(ishelp)
            {
                var checkhelp = ps.Find(x => x.category.Equals(category) && (x.setting.Equals(setting) || x.synonym.Equals(setting)));
                if(checkhelp == null)
                {
                    this.lastmessage = $"No help is available for category '{category}' and option '{setting}'.";
                    return false;
                }
                checkhelp.isactive = true;
                checkhelp.ishelprequest = true;
                return true;
            }

            // go through each parameter, try to determine what each is, validate its form, and set the relevant properties on the ps object
            bool skipnext = false;
            for(int i = 0; i < parameters.Count; i++)
            {
                // if the first one, just find the category help and set active
                if(skipnext)
                {
                    // do nothing here
                    skipnext = false;
                }
                else if(i==0)
                {
                    var categorysetting = ps.Find(x => x.category.Equals(category) && (x.setting.Equals("--help")));
                    if(categorysetting==null)
                    {
                        this.lastmessage = $"Cannot locate category '{category}' in parameters.";
                        return false;
                    }
                    categorysetting.isactive = true;
                }
                else
                {
                    // if a parameter is passed-in, if it's not --help|-h (which is analysed and finalised above) and/or the category 
                    // (again which is determined above) then it is basically one of three things:
                    // 1 a recognised command (like -f|--filename)
                    // 2 is something following a recognised command that isn't a switch
                    // 3 is in a specific order in the parameter listing (an ordinal command)

                    setting = parameters[i].Trim().ToLower();
                    var checksetting = ps.Find(x => x.category.Equals(category) && (x.setting.Equals(setting) || x.synonym.Equals(setting)));
                    bool isordinal = false;
                    bool issetting = false;
                    bool isswitch = false;
                    ParameterSetting? currentsetting; 
                    if(checksetting==null)
                    {
                        // check the ordinal - this might be okay if in a specific place in the parameters
                        var ordinalsetting = ps.Find(x => x.category.Equals(category) && (x.ordinal.Equals(i+1)));
                        if(ordinalsetting==null)
                        {
                            this.lastmessage = $"Parameter '{parameters[i]}' is incorrect.  Enter {this.appname} {category} --help for more information.";
                            return false;
                        }
                        isordinal = true;
                        currentsetting = ordinalsetting;
                    }
                    else
                    {
                        issetting = true;
                        currentsetting = checksetting;
                    }
                    isswitch = (currentsetting.paratype == ParameterType.Switch);
                    // if this is an ordinal, we validate it directly
                    // if this is a switch, we do not validate at all
                    // if this is a setting, we validate the next parameter using the 'nextxxx' properties on the current setting object
                    string validatetext = "";
                    if(isswitch)
                    {
                        validatetext = "";
                    }
                    else if(isordinal)
                    {
                        validatetext = this.ValidateParameter(parameters[i],checksetting!.description,checksetting!.paratype,checksetting!.paraintmin,checksetting!.paraintmax,checksetting!.paraseparator);
                    }
                    else if(issetting)
                    {
                        // try to grab the next parameter
                        if(i == (parameters.Count - 1))
                        {
                            this.lastmessage = $"Setting '{setting}' must be followed by an additional parameter.  Enter {this.appname} {category} {setting} --help for more information.";
                            return false;
                        }
                        validatetext = this.ValidateParameter(parameters[i+1],checksetting!.description,checksetting!.nextparatype,checksetting!.nextparaintmin,checksetting!.nextparaintmax,checksetting!.nextparaseparator);
                    }

                    if(validatetext != "")
                    {
                        this.lastmessage = validatetext;
                        return false;
                    }

                    // now set the relevant properties of the parameter object
                    if(isswitch)
                    {
                        checksetting!.isactive = true;
                        checksetting!.input = parameters[i];
                        checksetting!.nextisactive = false;
                        checksetting!.nextinput = "";
                    }
                    else if(isordinal)
                    {
                        checksetting!.isactive = true;
                        checksetting!.input = parameters[i];
                        checksetting!.nextisactive = false;
                        checksetting!.nextinput = "";
                    }
                    else if(issetting)
                    {
                        checksetting!.isactive = true;
                        checksetting!.input = parameters[i];
                        checksetting!.nextisactive = true;
                        checksetting!.nextinput = parameters[i+1];
                        skipnext = true;
                    }
                }
            }
            return true;
        }

        public string ValidateParameter(string parameter, string description, ParameterType paratype, int paraintmin = 0, int paraintmax = 0, string paraseparator = "")
        {
            if(paraseparator != "" && parameter.Contains(paraseparator))
            {
                string[] fields = parameter.Split(paraseparator,StringSplitOptions.None);
                string errormessage = "";
                foreach(var field in fields)
                {
                    errormessage = this.ValidateParameter(field,description,paratype,paraintmin,paraintmax);
                    if(errormessage != "")
                        return errormessage;
                }
                return "";
            }

            string output = "";
            if(paratype == ParameterType.Any)
            {

            }
            else if(paratype == ParameterType.Switch)
            {

            }
            else if(paratype == ParameterType.Input)
            {

            }
            else if(paratype == ParameterType.Text)
            {

            }
            else if(paratype == ParameterType.File)
            {

            }
            else if(paratype == ParameterType.Integer)
            {
                if(parameter=="")
                    return $"{description} parameter must be a number between {paraintmin} and {paraintmax}.";
                if(parameter.Contains(' '))
                    return $"{description} parameter value '{parameter}' cannot contain spaces.";
                if(parameter.StartsWith('-') && paraintmin >= 0 )
                    return $"{description} parameter value '{parameter}' cannot be negative.";
                if(parameter.Contains('.'))
                    return $"{description} parameter value '{parameter}' must be an integer.";
                string checkparameter = new String(parameter.Where(Char.IsDigit).ToArray());
                if(parameter != checkparameter)
                    return $"{description} parameter value '{parameter}' must only contain digits from 0-9.";
                if(checkparameter.Length >= Int32.MaxValue.ToString().Length)
                    return $"{description} parameter value '{parameter}' is too many digits.";
                int parameternum = Int32.Parse(checkparameter);
                if(parameternum < paraintmin)
                    return $"{description} parameter value '{parameter}' is less than {paraintmin}.";
                if(parameternum > paraintmax)
                    return $"{description} parameter value '{parameter}' is more than {paraintmax}.";
            }
            else if(paratype == ParameterType.HtmlFieldName)
            {
                if(parameter=="")
                    return $"{description} parameter cannot be blank.";
                if(parameter.Contains(' '))
                    return $"{description} parameter value '{parameter}' cannot contain spaces.";
                if(parameter.StartsWith('_'))
                    return $"{description} parameter value '{parameter}' cannot start with an underscore.";
                if(parameter.EndsWith('_'))
                    return $"{description} parameter value '{parameter}' cannot end with an underscore.";
                if(Char.IsDigit(parameter[0]))
                    return $"{description} parameter value '{parameter}' cannot start with a digit.";
                if(parameter.Contains('.'))
                    return $"{description} parameter value '{parameter}' cannot contain the period character.";
                string nounderscores = parameter.Replace("_","");
                string checkparameter = new String(nounderscores.Where(Char.IsLetterOrDigit).ToArray());
                if(nounderscores != checkparameter)
                    return $"{description} parameter value '{parameter}' must only contain letters, digits, and underscores.";
            }
            else if(paratype == ParameterType.CsFieldName || paratype == ParameterType.CsNameSpace)
            {
                if(parameter=="")
                {
                    if(paratype == ParameterType.CsFieldName)
                    {
                        return $"{description} parameter cannot be blank.";
                    }
                    return "";
                }
                if(parameter.Contains(' '))
                    return $"{description} parameter value '{parameter}' cannot contain spaces.";
                // if(parameter.StartsWith('_'))
                //     return $"{description} parameter value '{parameter}' cannot start with an underscore.";
                if(parameter.EndsWith('_'))
                    return $"{description} parameter value '{parameter}' cannot end with an underscore.";
                if(Char.IsDigit(parameter[0]))
                    return $"{description} parameter value '{parameter}' cannot start with a digit.";
                if(parameter.Contains('.'))
                {
                    if(paratype == ParameterType.CsFieldName)
                        return $"{description} parameter value '{parameter}' cannot contain the period character.";
                    if(parameter.Contains(".."))
                        return $"{description} parameter value '{parameter}' cannot contain consecutive period characters.";
                    if(parameter.StartsWith("."))
                        return $"{description} parameter value '{parameter}' cannot start with a period character.";
                    if(parameter.EndsWith("."))
                        return $"{description} parameter value '{parameter}' cannot end with a period character.";

                    string[] classnames = parameter.Split('.',StringSplitOptions.None);
                    foreach(var classname in classnames)
                    {
                        string checktext = this.ValidateParameter(classname, description, ParameterType.CsClassName, paraintmin, paraintmax, paraseparator);
                        if(checktext != "")
                            return checktext;
                    }
                }
                    
                string nounderscores = parameter.Replace("_","");
                string checkunderscores = new String(nounderscores.Where(Char.IsLetterOrDigit).ToArray());
                if(nounderscores != checkunderscores && paratype == ParameterType.CsFieldName)
                {
                    return $"{description} parameter value '{parameter}' must only contain letters, digits, and underscores.";
                }
                string noperiods = parameter.Replace(".","");
                string checknamespace = new String(noperiods.Where(Char.IsLetterOrDigit).ToArray());
                if(noperiods != checknamespace && paratype == ParameterType.CsNameSpace)
                {
                    return $"{description} parameter value '{parameter}' must only contain letters, digits, and periods.";
                }


                if(this.reservedcs.Contains(','+parameter+','))
                    return $"The value '{parameter}' is a reserved word in C# and cannot be used.";

            }

            return output;
        }

        public bool IsHelpRequested()
        {
            var checkhelp = ps.Find(x => x.ishelprequest.Equals(true));
            return (checkhelp != null);
        }
        public List<string> GetHelp()
        {
            var checkhelp = ps.Find(x => x.ishelprequest.Equals(true));
            if(checkhelp == null)
                return new List<string>();
            return checkhelp!.helptext;
        }

    }
    public class ParameterSetting
    {
        public string category = "";
        public string setting = "";
        public string synonym = "";
        public string description = "";
        public string restriction = "";
        public string input = "";
        public bool isactive = false;
        public bool ishelprequest = false;
        public string nextinput = "";
        public bool nextisactive = false;
        public bool required  = false;
        public int ordinal = 0;
        public List<string> helptext = new List<string>();
        public ParameterType paratype = ParameterType.Any; // Switch, Input, Any, File, Integer, Text, CsFieldName, CsFieldInfo, CsClassName, HtmlFieldInfo, HtmlFieldName
        public int paraintmin = 0; 
        public int paraintmax = 65535;
        public string paraseparator = ""; 
        public ParameterType nextparatype = ParameterType.Any; // Switch, Input, Any, File, Integer, Text, CsFieldName, CsFieldInfo, CsClassName, HtmlFieldInfo, HtmlFieldName
        public int nextparaintmin = 0; 
        public int nextparaintmax = 65535; 
        public string nextparaseparator = ""; 
    }

    public enum ParameterType
    {
        Any,
        Switch,
        Input,
        File,
        Integer,
        Text,
        CsFieldName,
        CsFieldInfo,
        CsClassName,
        CsNameSpace,
        CssName,
        HtmlFieldName,
        HtmlFieldType
    }
}