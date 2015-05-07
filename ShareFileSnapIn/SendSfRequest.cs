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
                eq,
                ne,
                gt,
                ge,
                lt,
                le,
                and,
                or,
                not,
                add,
                sub,
                mul,
                div,
                mod,

                startswith,
                endswith,
                substr,
                type
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
                SplitText(FilterBody);
                return GetFilter();
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
                            result = match.Value.Split(sepSpace, 3, StringSplitOptions.RemoveEmptyEntries);

                            propertyName = result[0].Trim();
                            filter = result[1].Trim();
                            propertyValue = result[2].Trim();

                            filterText = filterText.Substring(match.Length).Trim();
                            if (!string.IsNullOrWhiteSpace(filterText))
                            {
                                SplitText(filterText);
                            }
                        }
                    }

                    propertyValue = RemoveQuotes(propertyValue);

                    OperatorsStack.Push(filter);
                    PropertiesStack.Push(propertyName);
                    PropertiesStack.Push(propertyValue);
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
                Regex exp = new Regex(@"^\w+\s*(eq|ne|lt|le|gt|ge)\s*(?:"".*?""|'.*?'|(\d+(\.\d+)?)\b)");
                return exp.Match(filterText);
            }

            private Match CheckIfBracesOperation(string filterText)
            {
                Regex exp = new Regex(@"^(endswith|startswith)\(\w+\s*\,\s*(?:"".*?""|'.*?'|(\d+(\.\d+)?)\b)\)");
                return exp.Match(filterText);
            }

            private IFilter GetFilter()
            {
                IFilter filterType = null;

                Stack<IFilter> Operations = new Stack<IFilter>();

                while (OperatorsStack.Count > 0)
                {
                    string propertyName = null;
                    string propertyValue = null;
                    IFilter left = null;
                    IFilter right = null;
                    string op = OperatorsStack.Pop();
                    OperatorType operators = (OperatorType)Enum.Parse(typeof(OperatorType), op, true);

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

                        case OperatorType.and:
                            right = Operations.Pop();
                            left = Operations.Pop();
                            filterType = new AndFilter(left, right);
                            break;

                        case OperatorType.or:
                            right = Operations.Pop();
                            left = Operations.Pop();
                            filterType = new OrFilter(left, right);
                            break;

                        case OperatorType.not:
                            right = Operations.Pop();
                            filterType = new NotFilter(right);
                            break;
                    }

                    Operations.Push(filterType);
                }

                return Operations.Pop();
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

        }
    }
}
