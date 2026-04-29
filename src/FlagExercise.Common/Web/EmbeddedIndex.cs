namespace FlagExercise.Common.Web;

// Single-file React UI served by each service. React + htm are loaded from a CDN
// so there is no build step.
public static class EmbeddedIndex
{
    public static string Html(string role)
    {
        var title = role.Equals("Tx", StringComparison.OrdinalIgnoreCase)
            ? "T(x) - Source / Mover Service"
            : "R(x) - Destination / Deleter Service";
        return TEMPLATE
            .Replace("__ROLE__", role)
            .Replace("__TITLE__", title);
    }

    // Language=HTML
    private const string TEMPLATE = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width,initial-scale=1" />
<title>__TITLE__</title>
<link rel="icon" href="data:," />
<style>
  *,*::before,*::after { box-sizing: border-box; }
  body {
    margin: 0; font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif;
    background: #0f172a; color: #e2e8f0;
  }
  .wrap { max-width: 1100px; margin: 0 auto; padding: 24px; }
  header {
    display: flex; align-items: center; justify-content: space-between;
    flex-wrap: wrap; gap: 12px; margin-bottom: 20px;
  }
  h1 { margin: 0 0 4px 0; font-size: 22px; }
  .sub { color: #94a3b8; font-size: 13px; }
  .pill { font-size: 12px; padding: 2px 10px; border-radius: 999px; font-weight: 600; }
  .pill.run { background: #059669; color: #ecfdf5; }
  .pill.stop { background: #dc2626; color: #fee2e2; }
  .btn {
    cursor: pointer; border: 0; border-radius: 6px; color: white; font-weight: 600;
    padding: 6px 12px; font-size: 13px; margin-left: 6px;
  }
  .btn.start   { background: #059669; } .btn.start:hover   { background: #10b981; }
  .btn.stop    { background: #e11d48; } .btn.stop:hover    { background: #f43f5e; }
  .btn.restart { background: #d97706; } .btn.restart:hover { background: #f59e0b; }
  .btn.save    { background: #2563eb; padding: 8px 18px; font-size: 14px; }
  .btn.save:hover { background: #3b82f6; }

  .grid { display: grid; gap: 20px; grid-template-columns: 1fr; }
  @media (min-width: 900px) { .grid { grid-template-columns: 1fr 1fr; } }

  .card {
    background: #1e293b; border: 1px solid #334155; border-radius: 12px;
    padding: 18px;
  }
  .card h2 { margin: 0 0 14px 0; font-size: 16px; }

  .field { margin-bottom: 12px; }
  .field .lbl {
    display: block; font-size: 12px; color: #94a3b8; margin-bottom: 4px;
  }
  .field input[type=text], .field input[type=number], .field input[type=password],
  .field select, .field textarea {
    width: 100%; padding: 8px 10px; border-radius: 6px; font-size: 14px;
    color: #0f172a;           /* <-- black text, visible */
    background: #ffffff;      /* <-- white background */
    border: 1px solid #cbd5e1;
  }
  .field input:focus, .field select:focus, .field textarea:focus {
    outline: 2px solid #38bdf8; outline-offset: 0;
  }
  .field input.invalid, .field select.invalid {
    border-color: #ef4444; outline: 2px solid #ef4444;
  }
  .field .err { color: #fca5a5; font-size: 12px; margin-top: 4px; }

  .check { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; font-size: 14px; }
  .check input { width: 16px; height: 16px; }

  .row2 { display: grid; gap: 10px; grid-template-columns: 1fr 1fr; }
  .row3 { display: grid; gap: 10px; grid-template-columns: 2fr 1fr 1fr; }

  pre.box {
    margin: 0; background: #0b1220; border-radius: 8px; padding: 10px;
    font-size: 12px; color: #cbd5e1; max-height: 260px; overflow: auto;
    white-space: pre-wrap; word-break: break-all;
  }
  .logline { font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 11px; line-height: 1.5; }

  .server-errors {
    background: #7f1d1d; color: #fee2e2; border-radius: 8px; padding: 10px 14px;
    margin-top: 14px; font-size: 14px;
  }
  .server-errors ul { margin: 6px 0 0 18px; padding: 0; }
  .saved-ok {
    background: #065f46; color: #ecfdf5; border-radius: 8px; padding: 8px 14px;
    margin-top: 14px; font-size: 14px;
  }
  .save-bar { margin-top: 16px; display: flex; align-items: center; gap: 12px; }
</style>
</head>
<body>
<div id="root"></div>

<script crossorigin src="https://unpkg.com/react@18.3.1/umd/react.production.min.js"></script>
<script crossorigin src="https://unpkg.com/react-dom@18.3.1/umd/react-dom.production.min.js"></script>
<script crossorigin src="https://unpkg.com/htm@3.1.1/dist/htm.umd.js"></script>
<script>
(function(){
  // -------- helpers --------
  var h = React.createElement;
  var html = htm.bind(h);

  var ROLE = "__ROLE__";
  var IS_TX = ROLE === "Tx";

  function getJson(url) {
    return fetch(url).then(function(r){ return r.json(); });
  }
  function postJson(url, body) {
    return fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    }).then(function(r){
      return r.json().catch(function(){ return {}; }).then(function(data){
        return { ok: r.ok, data: data };
      });
    });
  }

  function isEmail(s) {
    return typeof s === "string" && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s);
  }

  // Client-side validation. Must match AppConfig.Validate on the server.
  function clientValidate(cfg) {
    var e = {};
    if (!cfg.destinationFolder) e.destinationFolder = "Required";
    if (IS_TX && !cfg.sourceFolder) e.sourceFolder = "Required";
    if (IS_TX && cfg.sourceFolder && cfg.destinationFolder &&
        cfg.sourceFolder === cfg.destinationFolder) {
      e.destinationFolder = "Must differ from source";
    }
    if (!(cfg.pollIntervalMs >= 250 && cfg.pollIntervalMs <= 600000)) {
      e.pollIntervalMs = "Must be between 250 and 600000 ms";
    }
    if (IS_TX) {
      if (!(cfg.flagCreateMinSeconds >= 1)) e.flagCreateMinSeconds = "Must be >= 1";
      if (cfg.flagCreateMaxSeconds < cfg.flagCreateMinSeconds) {
        e.flagCreateMaxSeconds = "Must be >= min";
      }
    }
    if (cfg.smtpEnabled) {
      if (!cfg.smtpHost) e.smtpHost = "Required";
      if (!(cfg.smtpPort >= 1 && cfg.smtpPort <= 65535)) e.smtpPort = "1..65535";
      if (!isEmail(cfg.smtpFrom)) e.smtpFrom = "Invalid email";
      if (!isEmail(cfg.smtpTo)) e.smtpTo = "Invalid email";
    }
    if (cfg.syslogEnabled) {
      if (!cfg.syslogHost) e.syslogHost = "Required";
      if (!(cfg.syslogPort >= 1 && cfg.syslogPort <= 65535)) e.syslogPort = "1..65535";
    }
    return e;
  }

  // -------- Field component --------
  // Renders a <label> + the input + an optional error message.
  // Adds an id to the input so we can jump/scroll to it on error.
  function Field(props) {
    var id = props.name;
    var inputEl = React.cloneElement(props.children, {
      id: id,
      name: id,
      className: (props.children.props.className || "") + (props.error ? " invalid" : "")
    });
    return html`
      <div class="field" id=${"f-" + id}>
        <label class="lbl" for=${id}>${props.label}</label>
        ${inputEl}
        ${props.error ? html`<div class="err">${props.error}</div>` : null}
      </div>
    `;
  }

  // -------- App --------
  function App() {
    var st = React.useState;

    var cfgState     = st(null);         var cfg = cfgState[0],     setCfg     = cfgState[1];
    var statusState  = st({});           var status = statusState[0], setStatus = statusState[1];
    var logsState    = st([]);           var logs = logsState[0],   setLogs    = logsState[1];
    var errsState    = st({});           var errs = errsState[0],   setErrs    = errsState[1];
    var savedState   = st(false);        var saved = savedState[0], setSaved   = savedState[1];
    var serverState  = st([]);           var serverErrs = serverState[0], setServerErrs = serverState[1];

    var refresh = React.useCallback(function(){
      getJson("/api/status").then(setStatus).catch(function(){});
      getJson("/api/logs?n=200").then(setLogs).catch(function(){});
    }, []);

    React.useEffect(function(){
      getJson("/api/config").then(setCfg);
    }, []);
    React.useEffect(function(){
      refresh();
      var t = setInterval(refresh, 2000);
      return function(){ clearInterval(t); };
    }, [refresh]);

    if (!cfg) return html`<div style=${{padding:"40px"}}>Loading...</div>`;

    function set(key, value) {
      setCfg(Object.assign({}, cfg, (function(){ var o={}; o[key]=value; return o; })()));
    }

    function scrollToFirstError(keys) {
      if (!keys.length) return;
      var first = keys[0];
      var box = document.getElementById("f-" + first);
      var el  = document.getElementById(first);
      if (box) box.scrollIntoView({ behavior: "smooth", block: "center" });
      if (el && el.focus) setTimeout(function(){ el.focus(); }, 250);
    }

    function save() {
      setSaved(false); setServerErrs([]);
      var e = clientValidate(cfg);
      setErrs(e);
      var keys = Object.keys(e);
      if (keys.length) { scrollToFirstError(keys); return; }

      postJson("/api/config", cfg).then(function(r){
        if (r.ok) { setSaved(true); return; }
        var list = (r.data && r.data.errors) || ["Save failed"];
        setServerErrs(list);
        // Jump to the first field the server complained about.
        var map = {
          "Source folder": "sourceFolder",
          "Destination folder": "destinationFolder",
          "Source and Destination": "destinationFolder",
          "Poll interval": "pollIntervalMs",
          "Flag min": "flagCreateMinSeconds",
          "Flag max": "flagCreateMaxSeconds",
          "SMTP host": "smtpHost",
          "SMTP port": "smtpPort",
          "SMTP From": "smtpFrom",
          "SMTP To": "smtpTo",
          "Syslog host": "syslogHost",
          "Syslog port": "syslogPort"
        };
        var target = [];
        list.forEach(function(msg){
          Object.keys(map).forEach(function(k){
            if (msg.indexOf(k) === 0) target.push(map[k]);
          });
        });
        scrollToFirstError(target);
      });
    }

    function ctl(action) {
      postJson("/api/control", { action: action }).then(refresh);
    }

    var running = !!status.running;

    return html`
      <div class="wrap">
        <header>
          <div>
            <h1>__TITLE__</h1>
            <div class="sub">Role: <b>${ROLE}</b> &middot; Host: ${status.machine || "-"}</div>
          </div>
          <div>
            <span class=${"pill " + (running ? "run" : "stop")}>${running ? "RUNNING" : "STOPPED"}</span>
            <button class="btn start"   onClick=${function(){ ctl("start"); }}>Start</button>
            <button class="btn stop"    onClick=${function(){ ctl("stop"); }}>Stop</button>
            <button class="btn restart" onClick=${function(){ ctl("restart"); }}>Restart</button>
          </div>
        </header>

        <div class="grid">
          <section class="card">
            <h2>Folders &amp; Timers</h2>
            ${IS_TX ? html`
              <${Field} name="sourceFolder" label="Source folder" error=${errs.sourceFolder}>
                <input type="text" value=${cfg.sourceFolder || ""}
                       onChange=${function(e){ set("sourceFolder", e.target.value); }} />
              </${Field}>
            ` : null}
            <${Field} name="destinationFolder" label="Destination folder" error=${errs.destinationFolder}>
              <input type="text" value=${cfg.destinationFolder || ""}
                     onChange=${function(e){ set("destinationFolder", e.target.value); }} />
            </${Field}>
            <${Field} name="pollIntervalMs" label="Folder poll interval (ms)" error=${errs.pollIntervalMs}>
              <input type="number" value=${cfg.pollIntervalMs}
                     onChange=${function(e){ set("pollIntervalMs", parseInt(e.target.value || "0", 10)); }} />
            </${Field}>
            ${IS_TX ? html`
              <div class="row2">
                <${Field} name="flagCreateMinSeconds" label="Flag min (sec)" error=${errs.flagCreateMinSeconds}>
                  <input type="number" value=${cfg.flagCreateMinSeconds}
                         onChange=${function(e){ set("flagCreateMinSeconds", parseInt(e.target.value || "0", 10)); }} />
                </${Field}>
                <${Field} name="flagCreateMaxSeconds" label="Flag max (sec)" error=${errs.flagCreateMaxSeconds}>
                  <input type="number" value=${cfg.flagCreateMaxSeconds}
                         onChange=${function(e){ set("flagCreateMaxSeconds", parseInt(e.target.value || "0", 10)); }} />
                </${Field}>
              </div>
            ` : null}
            <div class="check">
              <input id="se" type="checkbox" checked=${!!cfg.serviceEnabled}
                     onChange=${function(e){ set("serviceEnabled", e.target.checked); }} />
              <label for="se">Service enabled (master switch)</label>
            </div>
          </section>

          <section class="card">
            <h2>SMTP (email notifications)</h2>
            <div class="check">
              <input id="smtpe" type="checkbox" checked=${!!cfg.smtpEnabled}
                     onChange=${function(e){ set("smtpEnabled", e.target.checked); }} />
              <label for="smtpe">SMTP enabled</label>
            </div>
            <div class="row3">
              <${Field} name="smtpHost" label="Host" error=${errs.smtpHost}>
                <input type="text" value=${cfg.smtpHost || ""}
                       onChange=${function(e){ set("smtpHost", e.target.value); }} />
              </${Field}>
              <${Field} name="smtpPort" label="Port" error=${errs.smtpPort}>
                <input type="number" value=${cfg.smtpPort}
                       onChange=${function(e){ set("smtpPort", parseInt(e.target.value || "0", 10)); }} />
              </${Field}>
              <${Field} name="smtpUseSsl" label="SSL">
                <select value=${cfg.smtpUseSsl ? "1" : "0"}
                        onChange=${function(e){ set("smtpUseSsl", e.target.value === "1"); }}>
                  <option value="0">No</option>
                  <option value="1">Yes</option>
                </select>
              </${Field}>
            </div>
            <${Field} name="smtpFrom" label="From" error=${errs.smtpFrom}>
              <input type="text" value=${cfg.smtpFrom || ""}
                     onChange=${function(e){ set("smtpFrom", e.target.value); }} />
            </${Field}>
            <${Field} name="smtpTo" label="To" error=${errs.smtpTo}>
              <input type="text" value=${cfg.smtpTo || ""}
                     onChange=${function(e){ set("smtpTo", e.target.value); }} />
            </${Field}>
            <div class="row2">
              <${Field} name="smtpUser" label="User">
                <input type="text" value=${cfg.smtpUser || ""}
                       onChange=${function(e){ set("smtpUser", e.target.value); }} />
              </${Field}>
              <${Field} name="smtpPassword" label="Password (use an App Password for Gmail)">
                <input type="password" value=${cfg.smtpPassword || ""}
                       onChange=${function(e){ set("smtpPassword", e.target.value); }} />
              </${Field}>
            </div>
          </section>

          <section class="card">
            <h2>Syslog</h2>
            <div class="check">
              <input id="syse" type="checkbox" checked=${!!cfg.syslogEnabled}
                     onChange=${function(e){ set("syslogEnabled", e.target.checked); }} />
              <label for="syse">Syslog enabled</label>
            </div>
            <div class="row2">
              <${Field} name="syslogHost" label="Host (IP/DNS)" error=${errs.syslogHost}>
                <input type="text" value=${cfg.syslogHost || ""}
                       onChange=${function(e){ set("syslogHost", e.target.value); }} />
              </${Field}>
              <${Field} name="syslogPort" label="Port" error=${errs.syslogPort}>
                <input type="number" value=${cfg.syslogPort}
                       onChange=${function(e){ set("syslogPort", parseInt(e.target.value || "0", 10)); }} />
              </${Field}>
            </div>
            <${Field} name="logLevel" label="Log level">
              <select value=${cfg.logLevel || "Info"}
                      onChange=${function(e){ set("logLevel", e.target.value); }}>
                <option>Debug</option><option>Info</option><option>Warn</option><option>Error</option>
              </select>
            </${Field}>
          </section>

          <section class="card">
            <h2>Status</h2>
            <pre class="box">${JSON.stringify(status, null, 2)}</pre>
          </section>
        </div>

        <div class="save-bar">
          <button class="btn save" onClick=${save}>Save configuration</button>
          ${saved ? html`<span class="saved-ok">Saved.</span>` : null}
        </div>
        ${serverErrs.length ? html`
          <div class="server-errors">
            <div><b>The server rejected the configuration:</b></div>
            <ul>
              ${serverErrs.map(function(m, i){ return html`<li key=${i}>${m}</li>`; })}
            </ul>
          </div>
        ` : null}

        <section class="card" style=${{marginTop:"20px"}}>
          <h2>Live log (last 200 lines)</h2>
          <pre class="box logline">${logs.join("\n")}</pre>
        </section>

        <div class="sub" style=${{marginTop:"16px"}}>
          Tx UI is on port 5081, Rx UI is on port 5082. This page polls the service every 2 seconds.
        </div>
      </div>
    `;
  }

  ReactDOM.createRoot(document.getElementById("root")).render(h(App));
})();
</script>
</body>
</html>
""";
}
