using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MiniJSON;

public class TestWS : MonoBehaviour {
	WebSocketWrapper ws;
    string FireBaseServer = "glaring-torch-9586";
    int version = 5;
	// Use this for initialization
	void Start () {
        StartCoroutine( CallWS() );
		ws = new WebSocketWrapper(string.Format("wss://{0}.firebaseio.com/.ws?v={1}", FireBaseServer, version));
		StartCoroutine(ws.Connect());
	}
	void Update(){
        if(ws!=null)
            ParseFireBaseCommand(ws.RecvString());
    }
    void OnApplicationQuit(){
        if(ws!=null) ws.QuickClose();
    }
    void ParseFireBaseCommand(string command){        
        if(command==null) return;
        var message = Json.Deserialize(command) as Dictionary<string,object>;
        var data = message["d"] as Dictionary<string,object>;
        if( message["t"] as string == "c") OnControl(data);
        else OnData(data);
    }
    void OnControl(Dictionary<string,object> json){
        if(json.ContainsKey("d")){
            var type = json["t"] as string;
            var data = json["d"];
            switch(type){
                case "h": OnHello(data as Dictionary<string,object>); break;
                case "r": OnControlReset(data as string); break;
                case "e": OnError(data as string); break; // Control error!
//                case "n": break;// End Trasmision.
//                case "s": break;// Control shutdown.
//                case "o": break;// Control pong!
                default:
                    Debug.LogError("Comando "+type+" desconocido.");
                    break;
            }
        }
    }
    void OnError(string error) {
        Debug.LogError(">>>>> "+error);
    }
    void OnHello(Dictionary<string,object> payload){        
        var timestamp = (long)payload["ts"];
        var version = payload["v"] as string;
        var host = payload["h"] as string;
        var sessionId = payload["s"] as string;
        WriteCommand("s",  Json.Deserialize("{\"c\":{\"sdk.unity.1-0-0\":1}}") as Dictionary<string,object>); 
//        
        StartCoroutine( LoginUser("Jhon@ibm.com", "123", ()=>{
            SetListen("/sessions");
            string node ="/sessions/"+GetHash();
            Dictionary<string,object> val = new Dictionary<string,object>();
            val.Add("userID","123"); 
            val.Add("userState","Ready");
            PutNode(node, val);
            ObservableNode(node);
        }) );
    }
    void OnControlReset(string data){
        // No es necesario cerra la conexion antigua, ya la cierra el servidor.
		ws = new WebSocketWrapper(string.Format( "wss://{0}/.ws?v={1}&ns={2}",data as string, version, FireBaseServer));
		StartCoroutine(ws.Connect());
    }
    void OnData(Dictionary<string,object> json){
        var b = json.ContainsKey("b")?json["b"]:null;
        var r = (long)(json.ContainsKey("r")?json["r"]:0L);
        var a = json.ContainsKey("a")?json["a"] as string:"";
        var error = json.ContainsKey("error")?json["error"]:null;
        if(r!=0) OnResponse(r, b);
        else if(error!=null){
        }else {
            OnAction(a,b);
        }
    }
    void OnResponse(long id, object body){
//        Debug.Log("OnResponse("+id+")");
    }
    void OnAction(string action, object body){
        // action d,m,c,ac,sd
        switch(action){
            case "d":
                Debug.Log(">>>> OnAction("+action+") data "+Json.Serialize(body));
                break;
            default:
                Debug.LogError("Accion "+action+" desconocida.");
                break;
        }
    }
    void WriteCommand(string a, Dictionary<string,object> data){
        WriteCommand(a, Json.Serialize(data));
    }
    long requestID = 1; 
    void WriteCommand(string command, string data){
        // r: requestID, a: action, b: body
        ws.SendString(string.Format( "{{'t':'d','d':{{'r':{0},'a':'{1}','b':{2}}}}}", requestID++, command, data).Replace('\'','"'));
    }
    // Autentificación.
    public void AuthByToken(string token){
        // Command: 'auth'/'gauth' Params: cred: token
        WriteCommand( "auth", string.Format("{{'cred':'{0}'}}",token) );
    }
    // Listen
    // l y q parece ser lo mismo.
    public void SetListen(string path ){ // Falta el delegado para las modificaciones.
        // Command: 'q' Params: p: path, t: (tag)???, h: ???
        WriteCommand( "q", string.Format("{{'p':'{0}'}}",path) );
    } 
    // n: UnListen
    public void ResetListen(string path ){ // Falta el delegado para las modificaciones.
        // Command: 'n' Params: p: path, t: (tag)???, q: (queryObject)
        WriteCommand( "n", string.Format("{{'p':'{0}'}}",path) );
    }
    // Set
    public void PutNode(string path, object val){
        WriteCommand("p", string.Format("{{'p':'{0}','d':{1}}}",path, Json.Serialize(val)) );
    }
    // Update
    public void MergeNode(string path, object val) {
        WriteCommand("m", string.Format("{{'p':'{0}','d':{1}}}", path, Json.Serialize(val)) );
    }

    // s: Stats
    // o: onDisconectPut
    // om: onDisconnectMerge
    // oc: onDisconnectCancel
    public void ObservableNode(string path) {
        WriteCommand("o", string.Format("{{'p':'{0}','d':{1}}}", path, "null"));
    }
    public IEnumerator CreateUser(string login, string pass){
        string url = string.Format( "https://auth.firebase.com/v2/{0}/users?&email={1}&password={2}&_method=POST&v=js-2.2.9&transport=json&suppress_status_codes=true", FireBaseServer, WWW.EscapeURL(login), pass);
        WWW www = new WWW(url);
        yield return www;
        var message = Json.Deserialize(www.text) as Dictionary<string,object>; 
    }
    public IEnumerator LoginUser(string login, string pass, System.Action callback){
        string url = string.Format( "https://auth.firebase.com/v2/{0}/auth/password?&email={1}&password={2}&v=js-2.2.9&transport=json&suppress_status_codes=true", FireBaseServer, WWW.EscapeURL(login), pass);
        WWW www = new WWW(url);
        yield return www;
        var response = Json.Deserialize(www.text) as Dictionary<string,object>; 
        AuthByToken(response["token"] as string);
        if(callback!=null) callback();
    }
    string GetHash(){
        return System.Convert.ToBase64String(System.Guid.NewGuid().ToByteArray()).Replace("=","").Replace("+","").Replace("/","#");
    }


    public IEnumerator CallWS(){
        string url = "https://script.google.com/macros/s/AKfycbwdL24yiQDx8fjphs9dZOe1naGA0B_AEf84uoAhYJ_VVXTQrcF8/exec?command=test";
        WWW www = new WWW(url);
        yield return www;
        Debug.LogError(">>>>> "+www.text);
    }
    
 
}