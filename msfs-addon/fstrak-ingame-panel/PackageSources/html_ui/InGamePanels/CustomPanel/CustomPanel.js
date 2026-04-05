class IngamePanelCustomPanel extends TemplateElement {
    constructor() {
        super(...arguments);
        this.panelActive = false;
        this.started = false;
        this.ingameUi = null;
        this.simvarInterval = null;
        this.initialize();
    }
    connectedCallback() {
        super.connectedCallback();
        console.log('[FSTRaK] connectedCallback fired');
        console.log('[FSTRaK] typeof SimVar=' + typeof SimVar);
        console.log('[FSTRaK] typeof simVarManager=' + typeof simVarManager);
        console.log('[FSTRaK] typeof RegisterSimVar=' + typeof RegisterSimVar);
        console.log('[FSTRaK] typeof Simplane=' + typeof Simplane);
        var self = this;
        this.ingameUi = this.querySelector('ingame-ui');
        this.iframeElement = document.getElementById("CustomPanelIframe");
        this.m_MainDisplay = document.querySelector("#MainDisplay");
        this.m_MainDisplay.classList.add("hidden");
        this.m_Footer = document.querySelector("#Footer");
        this.m_Footer.classList.add("hidden");

        // Load the iframe and start polling unconditionally — the panel HTML only
        // runs while the toolbar panel is open, so panelActive is always true here.
        this.panelActive = true;
        if (this.iframeElement) {
            this.iframeElement.src = 'http://127.0.0.1:8765/panel';
        }
        this.startSimVarPolling();

        if (this.ingameUi) {
            this.ingameUi.addEventListener("panelActive", function(e) {
                self.panelActive = true;
                if (self.iframeElement && !self.iframeElement.src) {
                    self.iframeElement.src = 'http://127.0.0.1:8765/panel';
                }
                if (!self.simvarInterval) self.startSimVarPolling();
            });
            this.ingameUi.addEventListener("panelInactive", function(e) {
                self.panelActive = false;
                self.stopSimVarPolling();
                if (self.iframeElement) {
                    self.iframeElement.src = '';
                }
            });
        }
    }
    startSimVarPolling() {
        var self = this;
        if (this.simvarInterval) return;
        console.log('[FSTRaK] startSimVarPolling: starting interval');
        this.simvarInterval = setInterval(function() {
            if (!self.panelActive) {
                console.log('[FSTRaK] poll tick: panelActive=false, skipping');
                return;
            }
            try {
                if (typeof SimVar === 'undefined') {
                    console.error('[FSTRaK] SimVar still undefined in poll tick');
                    return;
                }
                var lat = SimVar.GetSimVarValue("PLANE LATITUDE", "degrees");
                var lon = SimVar.GetSimVarValue("PLANE LONGITUDE", "degrees");
                var hdg = SimVar.GetSimVarValue("PLANE HEADING DEGREES MAGNETIC", "degrees");
                var alt = SimVar.GetSimVarValue("INDICATED ALTITUDE", "feet");
                var spd = SimVar.GetSimVarValue("GPS GROUND SPEED", "knots");
                console.log('[FSTRaK] SimVars: lat=' + lat + ' lon=' + lon + ' hdg=' + hdg + ' alt=' + alt + ' spd=' + spd);
                var url = 'http://127.0.0.1:8765/simvar?lat=' + lat + '&lon=' + lon + '&hdg=' + hdg + '&alt=' + alt + '&spd=' + spd;
                fetch(url).then(function(resp) {
                    console.log('[FSTRaK] GET /simvar response: ' + resp.status);
                }).catch(function(err) {
                    console.error('[FSTRaK] GET /simvar failed: ' + err);
                });
            } catch(e) {
                console.error('[FSTRaK] SimVar read error: ' + e);
            }
        }, 1000);
    }
    stopSimVarPolling() {
        if (this.simvarInterval) {
            clearInterval(this.simvarInterval);
            this.simvarInterval = null;
        }
    }
    initialize() {
        if (this.started) return;
        this.started = true;
    }
    disconnectedCallback() {
        super.disconnectedCallback();
        this.stopSimVarPolling();
    }
}
window.customElements.define("ingamepanel-custom", IngamePanelCustomPanel);
checkAutoload();
