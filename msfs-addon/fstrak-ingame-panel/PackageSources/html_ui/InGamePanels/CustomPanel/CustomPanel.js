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
        var self = this;
        this.ingameUi = this.querySelector('ingame-ui');
        this.iframeElement = document.getElementById("CustomPanelIframe");
        this.m_MainDisplay = document.querySelector("#MainDisplay");
        this.m_MainDisplay.classList.add("hidden");
        this.m_Footer = document.querySelector("#Footer");
        this.m_Footer.classList.add("hidden");
        if (this.ingameUi) {
            this.ingameUi.addEventListener("panelActive", function(e) {
                self.panelActive = true;
                if (self.iframeElement) {
                    self.iframeElement.src = 'http://127.0.0.1:8765/panel';
                }
                self.startSimVarPolling();
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
        this.simvarInterval = setInterval(function() {
            if (!self.panelActive || !self.iframeElement || !self.iframeElement.contentWindow) return;
            try {
                var data = {
                    type: 'simvar',
                    lat: SimVar.GetSimVarValue("PLANE LATITUDE", "degrees"),
                    lon: SimVar.GetSimVarValue("PLANE LONGITUDE", "degrees"),
                    hdg: SimVar.GetSimVarValue("PLANE HEADING DEGREES MAGNETIC", "degrees"),
                    alt: SimVar.GetSimVarValue("INDICATED ALTITUDE", "feet"),
                    spd: SimVar.GetSimVarValue("GPS GROUND SPEED", "knots")
                };
                self.iframeElement.contentWindow.postMessage(data, '*');
            } catch(e) {}
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
