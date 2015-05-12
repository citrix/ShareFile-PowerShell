using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using ShareFile.Api;
using ShareFile.Api.Models;
using System.IO;
using ShareFile.Api.Client.Requests;
using Newtonsoft.Json;
using ShareFile.Api.Client.Exceptions;
using ShareFile.Api.Client.Requests.Filters;
using System.Text.RegularExpressions;

namespace ShareFile.Api.Powershell
{
    [Cmdlet(VerbsCommunications.Send, Noun)]
    public class SendSfRequest : PSCmdlet
    {
        private const string Noun = "SfRequest";

        [Parameter(
            Position = 0,
            Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public PSShareFileClient Client { get; set; }

        [Parameter(Position = 1)]
        public string Method { get; set; }

        [Parameter(Position = 2)]
        public Uri Uri { get; set; }

        [Parameter(Mandatory = false, Position = 3, ValueFromPipeline = true)]
        public ODataObject Body { get; set; }

        [Parameter]
        public string Entity { get; set; }

        [Parameter]
        public string Id { get; set; }

        [Parameter]
        public string Navigation { get; set; }

        [Parameter]
        public string Action { get; set; }

        [Parameter]
        public string Cast { get; set; }

        [Parameter]
        public System.Collections.Hashtable Parameters { get; set; }

        [Parameter]
        public string Expand { get; set; }

        [Parameter]
        public string Select { get; set; }

        [Parameter]
        public string Filter { get; set; }

        [Parameter]
        public string BodyText { get; set; }


        [Parameter]
        public string Account { get; set; }

        protected override void ProcessRecord()
        {
            if (Id != null && Uri != null) throw new Exception("Set only Id or Uri");
            if (Action == null && Cast != null) Action = Cast;
            if (Action == null && Navigation != null) Action = Navigation;
            if (Method == null) Method = "GET";
            Method = Method.ToUpper();

            Query<ODataObject> query = new Query<ODataObject>(Client.Client);
            query.HttpMethod = Method;

            if (string.IsNullOrWhiteSpace(Filter) == false) query.Filter(AddFilter());
            if (Entity != null) query = query.From(Entity);
            if (Action != null) query = query.Action(Action);
            if (Id != null) query = query.Id(Id);
            else if (Uri != null) query = query.Id(Uri.ToString());

            if (Parameters != null)
            {
                foreach (var key in Parameters.Keys)
                {
                    if (!(key is string)) throw new Exception("Use strings for parameter keys");
                    query = query.QueryString((string)key, Parameters[key].ToString());
                }
            }
            if (Expand != null) query = query.Expand(Expand);
            if (Select != null) query = query.Select(Select);
            if (Body != null)
            {
                query.Body = Body;
            }
            else if (BodyText != null)
            {
                query.Body = BodyText;
            }
            try
            {
                var response = query.Execute();
                if (response != null)
                {
                    Type t = response.GetType();
                    if (t.IsGenericType)
                    {
                        if (t.GetGenericTypeDefinition() == typeof(ODataFeed<>))
                        {
                            var feed = t.GetProperty("Feed").GetValue(response, null) as IEnumerable<ODataObject>;
                            foreach (var o in feed)
                            {
                                WriteObject(o);
                            }
                        }
                    }
                    else
                    {
                        WriteObject(response);
                    }
                }
            }
            catch (ODataException e)
            {
                WriteError(new ErrorRecord(new Exception(e.Code.ToString() + ": " + e.ODataExceptionMessage.Message), e.Code.ToString(), ErrorCategory.NotSpecified, query.GetEntity()));
            }
        }

        private IFilter AddFilter()
        {
            FilterBuilder builder = new FilterBuilder(Filter);
            return builder.Build();
        }

        private class FilterBuilder
        {
            private enum OperatorType
            {
                none,
                eq,// Equal to 
                ne,// Not Equal to
                gt,// Greater than
                ge,// Greater than or Equal to
                lt,// Less than
                le,// Less than or Equal to
                and,// logical And operator
                or,// logical Or operator
                not,// logical Not operator
                add,// arithmetic ADDITION operator
                sub,// arithmetic SUBTRACTION operator
                mul,// arithmetic Multiplication operator
                div,// arithmetic Division operator
                mod,// arithmetic Modulo operator
                startswith,// string Starts With operator
                endswith,// string Ends With operator
                substr,// string Sub String operator
                precedence // Precedence grouping (parenthesis)
            }

            private Stack<string> OperatorsStack;
            private Stack<string> PropertiesStack;
            private string FilterBody;

            public FilterBuilder(string filterBody)
            {
                OperatorsStack = new Stack<string>();
                PropertiesStack = new Stack<string>();
                FilterBody = filterBody;
            }

            public IFilter Build()
            {
                try
                {
                    SplitText(FilterBody);
                    return GetFilter();
                }
                catch (Exception ex)
                {
                    throw new Exception("Invalid filter, please check syntax and try again.", ex);
                }
            }

            private void SplitText(string filterText)
            {
                string[] sepSpace = { " " };
                string[] sepBraces = { "(", ",", ")" };
                string[] result = null;

                string propertyName = string.Empty;
                string filter = string.Empty;
                string propertyValue = string.Empty;

                Match match = CheckIfLogicalOperation(filterText);
                if (match.Success)
                {
                    OperatorsStack.Push(match.Value.Trim());
                    filterText = filterText.Substring(match.Length).Trim();
                    SplitText(filterText);
                }
                else
                {

                    match = CheckIfBracesOperation(filterText);
                    if (match.Success)
                    {
                        result = filterText.Split(sepBraces, 4, StringSplitOptions.RemoveEmptyEntries);

                        filter = result[0].Trim();
                        propertyName = result[1].Trim();
                        propertyValue = result[2].Trim();

                        if (result.Length == 4)
                        {
                            SplitText(result[3].Trim());
                        }
                    }
                    else
                    {
                        match = CheckIfComparisionOperation(filterText);
                        if (match.Success)
                        {
                            Regex expLeft = new Regex(@"^\w+\b");
                            Regex expOperator = new Regex(@"^((eq|ne|lt|le|gt|ge)\b|(=|==|!=|<>|<|<=|>|>=))");
                            Regex expRight = new Regex(@"^(?:"".*?""|'.*?'|[a-zA-Z0-9.]+)");

                            match = expLeft.Match(filterText);
                            propertyName = match.Value.Trim();
                            filterText = filterText.Substring(match.Length).Trim();

                            match = expOperator.Match(filterText);
                            filter = match.Value.Trim();
                            filterText = filterText.Substring(match.Length).Trim();

                            match = expRight.Match(filterText);
                            propertyValue = match.Value.Trim();
                            filterText = filterText.Substring(match.Length).Trim();

                            if (!string.IsNullOrWhiteSpace(filterText))
                            {
                                SplitText(filterText);
                            }
                        }
                        else
                        {
                            match = CheckIfArithmeticOperation(filterText);
                            if (match.Success)
                            {
                                Regex expLeft = new Regex(@"^\w+\b");
                                Regex expOperator = new Regex(@"^(add|sub|mul|div|mod)\b");

                                Regex innerExpLeft = new Regex(@"^(?:"".*?""|'.*?'|[a-zA-Z0-9.]+)");
                                Regex innerExpOperator = new Regex(@"^(((eq|ne|lt|le|gt|ge)\s+)|(\s*(=|==|!=|<>|<|<=|>|>=)\s*))\b");
                                Regex innerExpRight = new Regex(@"^(?:"".*?""|'.*?'|[a-zA-Z0-9.]+)");

                                match = expLeft.Match(filterText);
                                propertyName = match.Value.Trim();
                                filterText = filterText.Substring(match.Length).Trim();

                                match = expOperator.Match(filterText);
                                filter = match.Value.Trim();
                                filterText = filterText.Substring(match.Length).Trim();

                                match = innerExpLeft.Match(filterText);
                                propertyValue = match.Value.Trim();
                                filterText = filterText.Substring(match.Length).Trim();

                                match = innerExpOperator.Match(filterText);
                                string innerOperator = match.Value.Trim();
                                filterText = filterText.Substring(match.Length).Trim();

                                match = innerExpRight.Match(filterText);
                                string propertyValue2 = match.Value.Trim();
                                filterText = filterText.Substring(match.Length).Trim();

                                if (!string.IsNullOrWhiteSpace(filterText))
                                {
                                    SplitText(filterText);
                                }

                                OperatorsStack.Push(innerOperator);
                                PropertiesStack.Push(RemoveQuotes(propertyValue2));
                            }
                            else
                            {
                                throw new Exception("Invalid filter, please check syntax and try again.");
                            }
                        }
                    }

                    OperatorsStack.Push(filter);
                    PropertiesStack.Push(propertyName);
                    PropertiesStack.Push(RemoveQuotes(propertyValue));
                }
            }

            private string RemoveQuotes(string value)
            {
                return value.Trim("'\"".ToCharArray());
            }

            private Match CheckIfLogicalOperation(string filterText)
            {
                Regex exp = new Regex(@"^(and|or|not)\s");
                return exp.Match(filterText);
            }

            private Match CheckIfComparisionOperation(string filterText)
            {
                Regex exp = new Regex(@"^\w+((\s+(eq|ne|lt|le|gt|ge)\s+)|(\s*(=|==|!=|<>|<|<=|>|>=)\s*))(?:"".*?""|'.*?'|[a-zA-Z0-9.]+)");
                return exp.Match(filterText);
            }

            private Match CheckIfBracesOperation(string filterText)
            {
                Regex exp = new Regex(@"^(endswith|startswith)\(\w+\s*\,\s*(?:"".*?""|'.*?'|[a-zA-Z0-9.]+)\)");
                return exp.Match(filterText);
            }

            private Match CheckIfArithmeticOperation(string filterText)
            {
                Regex exp = new Regex(@"^\w+\s+(add|sub|mul|div|mod)\s+(?:"".*?""|'.*?'|[a-zA-Z0-9.]+)((\s+(eq|ne|lt|le|gt|ge)\s+)|(\s*(=|==|!=|<>|<|<=|>|>=)\s*))(?:"".*?""|'.*?'|[a-zA-Z0-9.]+)");
                return exp.Match(filterText);
            }

            private IFilter GetFilter()
            {
                IFilter filterType = null;

                Stack<IFilter> operations = new Stack<IFilter>();

                while (OperatorsStack.Count > 0)
                {
                    string propertyName = null;
                    string propertyValue = null;
                    IFilter left = null;
                    IFilter right = null;
                    string op = OperatorsStack.Pop();
                    OperatorType operators = GetOperator(op);

                    switch (operators)
                    {
                        case OperatorType.eq:
                        case OperatorType.ne:
                        case OperatorType.startswith:
                        case OperatorType.endswith:
                        case OperatorType.lt:
                        case OperatorType.le:
                        case OperatorType.gt:
                        case OperatorType.ge:
                            propertyValue = PropertiesStack.Pop();
                            propertyName = PropertiesStack.Pop();

                            filterType = CreateFilter(operators, propertyName, propertyValue);
                            break;

                        case OperatorType.add:
                        case OperatorType.sub:
                        case OperatorType.mul:
                        case OperatorType.div:
                        case OperatorType.mod:
                            propertyValue = PropertiesStack.Pop();
                            propertyName = PropertiesStack.Pop();
                            string propertyValue2 = PropertiesStack.Pop();
                            OperatorType operatorInner = GetOperator(OperatorsStack.Pop());

                            filterType = CreateFilter(operators, propertyName, propertyValue, operatorInner, propertyValue2);
                            break;

                        case OperatorType.and:
                            right = operations.Pop();
                            left = operations.Pop();
                            filterType = new AndFilter(left, right);
                            break;

                        case OperatorType.or:
                            right = operations.Pop();
                            left = operations.Pop();
                            filterType = new OrFilter(left, right);
                            break;

                        case OperatorType.not:
                            right = operations.Pop();
                            filterType = new NotFilter(right);
                            break;
                    }

                    operations.Push(filterType);
                }

                return operations.Pop();
            }

            private OperatorType GetOperator(string op)
            {
                if (Enum.IsDefined(typeof(OperatorType), op))
                {
                    return (OperatorType)Enum.Parse(typeof(OperatorType), op, true);
                }

                switch (op)
                {
                    case "=":
                    case "==":
                        return OperatorType.eq;
                    case "!=":
                    case "<>":
                        return OperatorType.ne;
                    case "<":
                        return OperatorType.le;
                    case "<=":
                        return OperatorType.lt;
                    case ">":
                        return OperatorType.gt;
                    case ">=":
                        return OperatorType.ge;
                    default:
                        return OperatorType.none;
                }
            }

            private IFilter CreateFilter(OperatorType operators, string propertyName, string propertyValue)
            {
                IFilter filter = null;
                switch (operators)
                {
                    case OperatorType.eq:
                        filter = new EqualToFilter(propertyName, propertyValue);
                        break;
                    case OperatorType.ne:
                        filter = new NotEqualToFilter(propertyName, propertyValue);
                        break;
                    case OperatorType.startswith:
                        filter = new StartsWithFilter(propertyName, propertyValue);
                        break;
                    case OperatorType.endswith:
                        filter = new EndsWithFilter(propertyName, propertyValue);
                        break;
                    case OperatorType.lt:
                        filter = new LessThanFilter(propertyName, propertyValue);
                        break;
                    case OperatorType.le:
                        filter = new LessThanOrEqualFilter(propertyName, propertyValue);
                        break;
                    case OperatorType.gt:
                        filter = new GreaterThanFilter(propertyName, propertyValue);
                        break;
                    case OperatorType.ge:
                        filter = new GreaterThanOrEqualFilter(propertyName, propertyValue);
                        break;
                }

                return filter;
            }

            private IFilter CreateFilter(OperatorType operators, string propertyName, string propertyValue1, OperatorType innerOperator, string propertyValue2)
            {
                IFilter filter = null;
                switch (operators)
                {
                    case OperatorType.add:
                        filter = new AdditionFilter(propertyName, propertyValue1, innerOperator.ToString(), propertyValue2);
                        break;
                    case OperatorType.sub:
                        filter = new SubtractionFilter(propertyName, propertyValue1, innerOperator.ToString(), propertyValue2);
                        break;
                    case OperatorType.mul:
                        filter = new MultiplicationFilter(propertyName, propertyValue1, innerOperator.ToString(), propertyValue2);
                        break;
                    case OperatorType.div:
                        filter = new DivisionFilter(propertyName, propertyValue1, innerOperator.ToString(), propertyValue2);
                        break;
                    case OperatorType.mod:
                        filter = new ModuloFilter(propertyName, propertyValue1, innerOperator.ToString(), propertyValue2);
                        break;
                }

                return filter;
            }
        }
    }
}
