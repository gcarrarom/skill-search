#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Web;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System;

public class Payload{
    public string text { get; set; }
    public List<Attachment> attachments { get; set;}
    public Payload(){
        this.text = null;
        this.attachments = new List<Attachment>();
    }
}

public class CallbackPayload{ 
    public string type { get; set;}
    public List<Action> actions { get; set;}
    public string callback_id { get; set;}
    public User user { get; set; }
}

public class User{
    public string id { get; set; }
    public string name { get; set; }
}

public class Attachment{
    public string text { get; set; }
    public string attachment_type { get; set; }
    public string fallback { get; set; }
    public string color = "#3AA3E3";
    public string callback_id { get; set; }
    public List<Action> actions { get; set; }
    public Attachment(string text){
        this.text = text;
    }
    public Attachment(string text, List<Action> actions){
        this.text = text;
        this.actions = actions;
        this.attachment_type = "default";
    }
    public Attachment(string text, string actionText, List<Option> options){
        this.text = text;
        this.actions = new List<Action>();
        this.actions.Add(new Action(actionText, options)); 
        this.attachment_type = "default";
    }    
    
    public Attachment(string callback, string text, string actionName, string actionText, List<Option> options){
        this.text = text;
        this.actions = new List<Action>();
        this.actions.Add(new Action(actionName, actionText, options));
        this.attachment_type = "default";
        this.callback_id = callback;
    }
    public Attachment(string callback, string attachmentText, string buttonName, string buttonText, string buttonValue){
        this.callback_id = callback;
        this.text = attachmentText;
        this.actions = new List<Action>();
        this.actions.Add(new Action(buttonName, buttonText, buttonValue));
        this.attachment_type = "default";
    }
}

public class Confirm
{
    public string title { get; set; }
    public string text { get; set; }
    public string ok_text { get; set; }
    public string dismiss_text { get; set; }
    public Confirm(string title, string text){
        this.title = title;
        this.text = text;
        this.ok_text = "Yes";
        this.dismiss_text = "No";
    }
    public Confirm(string title, string text, string ok_text, string dismiss_text){
        this.title = title;
        this.text = text;
        this.ok_text = ok_text;
        this.dismiss_text = dismiss_text;
    }
}

public class Action{
    public string name { get; set; }
    public string text { get; set; }
    public string type { get; set; }
    public List<Option> options { get; set; }
    public List<Option> selected_options { get; set;}
    public string value { get; set; }
    public Confirm confirm { get; set; }
    public Action(){
        this.type = "select";
        this.options = new List<Option>();
    }

    public Action(string text, List<Option> options){
        this.type = "select";
        this.options = options;
        this.text = text;
    }    

    public Action(string name, string text, List<Option> options){
        this.type = "select";
        this.options = options;
        this.text = text;
        this.name = name;
    }

    public Action(string buttonName, string buttonText, string buttonValue){
        this.type = "button";
        this.name = buttonName;
        this.text = buttonText;
        this.value = buttonValue;
    }
    public Action (string buttonName, string buttonText, string buttonValue, string confirmTitle, string confirmText){
        this.type = "button";
        this.name = buttonName;
        this.text = buttonText;
        this.value = buttonValue;
        this.confirm = new Confirm(confirmTitle, confirmText);
    }
}

public class Option{
    public Option(string text, string value){
        this.text = text;
        this.value = value;
    }
    public string text { get; set; }
    public string value { get; set; }
}

internal class Subject
{
    public string id { get; set; }
    public string name { get; set; }
    public List<string> consultants_ids { get; set; }
    public string _rid { get; set; }
    public string _self { get; set; }
    public string _etag { get; set; }
    public string _attachments { get; set; }
    public int _ts { get; set; }
    public Subject(string name){
        this.name = name;
        this.id = Guid.NewGuid().ToString();
        List<string> consultants_ids = new List<string>();
        this.consultants_ids = consultants_ids;
    }
}

internal class Consultant
{
    public string name { get; set; }
    public string slackID { get; set; }
    public string id { get; set;}
    public bool admin { get; set; }
}
[FunctionName("SkillsWebhook")]
public static async Task<IActionResult> Run(HttpRequest req,
                                    IEnumerable<Subject> inputSubjects,
                                    IEnumerable<Consultant> inputConsultants,
                                    IAsyncCollector<Subject> outputSubjects,
                                    IAsyncCollector<Consultant> outputConsultants,
                                    ILogger log)
{
    List<Subject> dbsubjects = new List<Subject>(inputSubjects);
    List<Consultant> dbConsultant = new List<Consultant>(inputConsultants);
    log.LogInformation("Some logs before using Cosmos");
    log.LogInformation($"Just got the information from Cosmos, here's the first Subject:{dbsubjects[0].name}");

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

    log.LogInformation($"Request Body:\n{requestBody}");
    
    NameValueCollection requestCollection = HttpUtility.ParseQueryString(requestBody);
    log.LogInformation($"Parsed the Request Body successfully");

    string outputText = null;
    string attachmentText = null;
    List<Option> subjects = new List<Option>();
    Payload payload = new Payload();
    string text = null;
    string requestingUserID = null;
    log.LogInformation($"payload={requestCollection["payload"]}");
    if(!String.IsNullOrEmpty(requestCollection["payload"])){
        CallbackPayload callbackPayload = JsonConvert.DeserializeObject<CallbackPayload>(requestCollection["payload"]);
        log.LogInformation($"Could Read the Callback ID: \"{callbackPayload.callback_id}\"");
        switch(callbackPayload.callback_id){
            case "skill_selection":{
                text = $"get {callbackPayload.actions[0].selected_options[0].value}";
                break;
            }
            case "assign_button":{
                text = $"assign {callbackPayload.actions[0].name} {callbackPayload.actions[0].value}";
                break;
            }
            case "create_button":{
                text = $"create {callbackPayload.actions[0].value}";
                break;
            }
            case "get_button":{
                text = $"get {callbackPayload.actions[0].value}";
                break;
            }
        }
        requestingUserID = callbackPayload.user.id;
    }else{
        text = requestCollection["text"].Trim();
        requestingUserID = requestCollection["user_id"].Trim();
    }
 
    List<string> commands = new List<string>(text.Split(' '));
    log.LogInformation($"Split of the commands, here's the first verb:{commands[0]}");

    string afterCommand = null;

    if(text.IndexOf(' ') != -1){
        afterCommand = text.Substring(text.IndexOf(' ') + 1);
    }

    switch (commands[0]){
        case "help":{
            log.LogInformation("Running a help Command");
            payload.text = "Use this slash command to return the Subject Matter Experts that can help you out!";
             
            attachmentText = @"/skills help - Displays this message
/skills list - Lists all the available Subjects/Skills 
/skills get <SubjectName> - Shows the list of Consultants that might be able to help you out;
/skills <ConsultantSlackTag - Shows all the skills from a consultant;
/skills create <Subjectname> - creates a Subject;
/skills assign <SubjectName> <ConsultantSlackTag> - Assigns a consultant to a Subject;
/skills remove <SubjectName> <ConsultantSlackTag> - Removes a consultant from a Subject;
/skills me - Lists the subjects that you are assigned to;
/skills dump - Lists all the subjects available;";
            payload.attachments.Add(new Attachment(attachmentText));

            break;
        }
        case "get":{
            log.LogInformation("Running a get Command");
            Guid subjectID;
            Subject subject = null;

            if(afterCommand==null){
                log.LogInformation("Nothing after the command...");
                payload.text = $"Subject name cannot be empty!";
                payload.attachments.Add(new Attachment("Use \"/skills list\" to list the subjects available"));
                break;
            }
            
            if(Guid.TryParse(afterCommand, out subjectID)){
                subject = dbsubjects.Find(x => String.Equals(x.id,afterCommand, StringComparison.OrdinalIgnoreCase));
            }else{
                subject = dbsubjects.Find(x => String.Equals(x.name,afterCommand, StringComparison.OrdinalIgnoreCase));
            }
            if(subject == null){
                payload.text = $"Subject {afterCommand} not found!";
                payload.attachments.Add(new Attachment("Use \"/skills list\" to list the subjects available"));
                break;
            }else if(subject.consultants_ids.Count() == 0){
                log.LogInformation("No Consultants in the list!");
                payload.text = $"There are no consultants in the Subject {subject.name}!";
                payload.attachments.Add(new Attachment("assign_button", "Would you like to assign youself as an expert for this subject?", subject.name, "Assign Myself", requestingUserID));
                break;
            }

            string consultantNames = "";

            Consultant consultant = dbConsultant.Find(x => String.Equals(x.id, subject.consultants_ids[0]));
            consultantNames += $"{consultant.name}, <@{consultant.slackID}>";
            if(subject.consultants_ids.Count()>1){
                for(int i = 1; i < subject.consultants_ids.Count();i++){
                    consultant = dbConsultant.Find(x => String.Equals(x.id, subject.consultants_ids[i]));
                    consultantNames += $"\n{consultant.name}, <@{consultant.slackID}>";
                }
            }
            payload.text = $"For the subject \"{subject.name}\", these people might be able to help!";
            payload.attachments.Add(new Attachment(consultantNames));

            break;
        }
        case "list":{
            log.LogInformation("Running a list Command");
            List<Option> options = new List<Option>();
            for(int i = 0 ; i < dbsubjects.Count() ; i++){
                log.LogInformation($"Adding {dbsubjects[i]} to the list");
                options.Add(new Option(dbsubjects[i].name, dbsubjects[i].id));
            }
            log.LogInformation("Setting output text");
            outputText = $"Here's the list of the subjects available in this tool:";
            attachmentText = "Choose a skill to query the list of consultants";
            
            log.LogInformation("Creating Payload");
            payload.text = outputText;
            payload.attachments.Add(new Attachment("skill_selection", attachmentText, "skill_list",  "Choose a Skill...", options));

            break;
        }
        case "create":{
            log.LogInformation("User Ran a create Command");

            if(afterCommand==null){
                payload.text = $"Subject name cannot be empty!";
                payload.attachments.Add(new Attachment("Use \"/skills help\" to get some help on how to use the commands."));
                break;
            }

            log.LogInformation($"Checking if the group \"{afterCommand}\" already exists");
            Subject subject = dbsubjects.Find(x => String.Equals(x.name,afterCommand, StringComparison.OrdinalIgnoreCase));
            if(subject != null){
                payload.text = $"The subject \"{afterCommand}\" already exists!";
                
            }
            else{
                subject = new Subject(afterCommand);
                await outputSubjects.AddAsync(subject);
                payload.text = $"Subject \"{afterCommand}\" created successfully!";
            }

            payload.attachments.Add(new Attachment("assign_button", "Would you like to assign youself as an expert for this subject?", subject.name, "Assign Myself", requestingUserID));

            break;
        }
        case "assign":{
            log.LogInformation("Assign command running...");

            if(afterCommand==null){
                payload.text = $"Subject name cannot be empty!";
                payload.attachments.Add(new Attachment("Use \"/skills help\" to get some help on how to use the commands."));
                break;
            }

            string user = commands.LastOrDefault();
            string userID = user;
            string subjectName = afterCommand.Substring(0, afterCommand.Count()-user.Count()).Trim();

            if(user[0] == '<'){
                userID = user.Split('|')[0].Substring(2);
            }

            log.LogInformation($"Assigning \"{userID}\" to \"{subjectName}\"");

            Consultant consultant = dbConsultant.Find(x=> String.Equals(x.slackID, userID, StringComparison.OrdinalIgnoreCase));

            if(consultant == null){
                payload.text = $"Consultant {user} not found!";
                payload.attachments.Add(new Attachment("Please make sure that you are referencing the Consultant with his Slack tag: i.e. <@U3X9GADFT>"));
                break;
            }
            Subject subject = dbsubjects.Find(x => String.Equals(x.name, subjectName, StringComparison.OrdinalIgnoreCase));

            if(subject == null){
                payload.text = $"Subject {subjectName} not found!";
                payload.attachments.Add(new Attachment("create_button", $"Would you like to create the subject \"{subjectName}\"?", "create_subject", "Create Subject", subjectName));
                break;
            }
            string existingID = subject.consultants_ids.Find(x => String.Equals(x, consultant.id, StringComparison.OrdinalIgnoreCase));
            
            log.LogInformation("Adding Consultant to the Subject");
            if(string.IsNullOrEmpty(existingID)){
                subject.consultants_ids.Add(consultant.id);
                await outputSubjects.AddAsync(subject);
                payload.text = $"Consultant {consultant.name} added to the subject {subjectName}";
            }else{
                payload.text = $"Consultant {consultant.name} already in the {subjectName} list";
            }
            payload.attachments.Add(new Attachment("get_button", $"Would you like to list the consultants in the subject \"{subjectName}\"?", "get_subject", "Get Consultants ", subjectName));


            break;
        }
        case "remove":{
            log.LogInformation("Remove Switch");
            
            string user = commands.LastOrDefault();
            string userID = user;
            string subjectName = afterCommand.Substring(0, afterCommand.Count()-user.Count()).Trim();

            if(user[0] == '<'){
                userID = user.Split('|')[0].Substring(2);
            }

            if(userID != requestingUserID){
                log.LogInformation($"Checking if \"{requestingUserID}\" has admin access to remove \"{userID}\".");
                Consultant requestingConsultant = dbConsultant.Find(x=> string.Equals(x.slackID, requestingUserID, StringComparison.OrdinalIgnoreCase));
                if(!requestingConsultant.admin){
                    payload.text = $"You don't have permissions to remove this user from the Subject {subjectName}!";
                    payload.attachments.Add(new Attachment("You can only remove yourself from subjects. Please contact an Admin if you wish to review a subject."));
                    break;
                }
            }
            
            log.LogInformation($"Removing \"{userID}\" from \"{subjectName}\"");

            Consultant consultant = dbConsultant.Find(x=> String.Equals(x.slackID, userID, StringComparison.OrdinalIgnoreCase));

            if(consultant == null){
                payload.text = $"Consultant {user} not found!";
                payload.attachments.Add(new Attachment("Please make sure that you are referencing the Consultant with his Slack tag: i.e. <@U3X9GADFT>"));
                break;
            }
            Subject subject = dbsubjects.Find(x => String.Equals(x.name, subjectName, StringComparison.OrdinalIgnoreCase));

            if(subject == null){
                payload.text = $"Subject {subjectName} not found!";
                payload.attachments.Add(new Attachment("create_button", $"Would you like to create the subject \"{subjectName}\"?", "create_subject", "Create Subject", subjectName));
                break;
            }
            string existingID = subject.consultants_ids.Find(x => String.Equals(x, consultant.id, StringComparison.OrdinalIgnoreCase));
            
            log.LogInformation("Removing Consultant from the Subject");
            if(!string.IsNullOrEmpty(existingID)){
                subject.consultants_ids.Remove(consultant.id);
                await outputSubjects.AddAsync(subject);
                payload.text = $"Consultant {consultant.name} removed from the subject {subjectName}";
            }else{
                payload.text = $"Consultant {consultant.name} wasn't in the {subjectName} Subject";
            }
            payload.attachments.Add(new Attachment("get_button", $"Would you like to list the consultants in the subject \"{subjectName}\"?", "get_subject", "Get Consultants ", subjectName));


            break;
        }
        case "me":{
            log.LogInformation("Default switch...");
            Consultant consultant = dbConsultant.Find(x=> String.Equals(x.slackID, requestingUserID, StringComparison.OrdinalIgnoreCase));
            if(consultant == null){
                payload.text = $"Consultant {requestingUserID} not found!";
                payload.attachments.Add(new Attachment("Please make sure that you are referencing the Consultant with his Slack tag: i.e. <@U3X9GADFT>"));
                break;
            }

            List<Subject> consultantSubjects = dbsubjects.FindAll(x=> x.consultants_ids.Find(y=> String.Equals(y, consultant.id, StringComparison.OrdinalIgnoreCase))!= null);  

            if(consultantSubjects.Count == 0){
                payload.text = "You are not assigned to any Subjects!";
                payload.attachments.Add(new Attachment("To add yourself to any subject, run the command /skills assign \"SubjectName\" \"YourSlackTag\".\nRun /skills help for more information."));
                break;
            }
            string subjectList = "";
            payload.text = $"You are assigned to {consultantSubjects.Count} Subjects! Here's the list:";
            subjectList += consultantSubjects[0].name;
            for(int i=1; i<consultantSubjects.Count(); i++){
                subjectList += $"\n{consultantSubjects[i].name}";
            }
            payload.attachments.Add(new Attachment(subjectList));

            break;
        }
        case "dump":{
            log.LogInformation($"Dump request by {requestingUserID}");

            string subjectList = string.Join("\n", dbsubjects.Select(x=> x.name));
                        
            payload.text = $"Here's the list of the subjects available in this tool:";
            payload.attachments.Add(new Attachment(subjectList));

            break;
        }
        case "link":{
            log.LogInformation("USer Requested a link");

            payload.text = "Work in progress...";
            payload.attachments.Add(new Attachment("Please come back whenever this feature is done :)"));

            break;
        }
        default:{
            log.LogInformation("Default switch...");

            string user = commands[0];
            string userID = user;

            if(user[0] == '<'){
                userID = user.Split('|')[0].Substring(2);
            }else{
                payload.text = $"Command {commands[0]} not found!";
                payload.attachments.Add(new Attachment("Use \"/skills help\" to list the commands available"));
                break;
            }

            Consultant consultant = dbConsultant.Find(x=> String.Equals(x.slackID, userID, StringComparison.OrdinalIgnoreCase));
            if(consultant == null){
                payload.text = $"Consultant {requestingUserID} not found!";
                payload.attachments.Add(new Attachment("Please make sure that you are referencing the Consultant with his Slack tag: i.e. <@U3X9GADFT>"));
                break;
            }

            List<Subject> consultantSubjects = dbsubjects.FindAll(x=> x.consultants_ids.Find(y=> String.Equals(y, consultant.id, StringComparison.OrdinalIgnoreCase))!= null);  

            if(consultantSubjects.Count == 0){
                payload.text = $"The Consultant <@{userID}> is not assigned to any Subjects!";
                payload.attachments.Add(new Attachment("To assign this consultant to any subject, run the command /skills assign \"SubjectName\" \"SlackTag\".\nRun /skills help for more information."));
                break;
            }
            string subjectList = "";
            payload.text = $"<@{userID}> is assigned to {consultantSubjects.Count} Subjects! Here's the list:";
            subjectList += consultantSubjects[0].name;
            for(int i=1; i<consultantSubjects.Count(); i++){
                subjectList += $"\n{consultantSubjects[i].name}";
            }
            payload.attachments.Add(new Attachment(subjectList));

            break;
        }
    }

    log.LogInformation($"outputText: {outputText}");

    string payloadJson = JsonConvert.SerializeObject(payload, Newtonsoft.Json.Formatting.None, 
                            new JsonSerializerSettings { 
                                NullValueHandling = NullValueHandling.Ignore
                            });
    log.LogInformation($"payloadJSON: {payloadJson}");
    log.LogInformation($"{requestingUserID} ran this command: {text}");

    return payload != null
        ? (ActionResult)new OkObjectResult(payload)
        : new BadRequestObjectResult("Use \"/skills help\" to list the commands available");
}
