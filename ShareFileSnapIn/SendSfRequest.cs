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
                            WriteObject(t.GetProperty("Feed").GetValue(response, null));
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
    }
}
