class IngamePanelCustomPanel extends TemplateElement {
    constructor() {
        super(...arguments);
        this.ingameUi = null;
        this.iframeElement = null;
        this.initialize();
    }
    connectedCallback() {
        super.connectedCallback();
        var self = this;
        this.ingameUi = this.querySelector('ingame-ui');
        this.iframeElement = document.getElementById("CustomPanelIframe");

        var mainDisplay = document.querySelector("#MainDisplay");
        if (mainDisplay) mainDisplay.classList.add("hidden");
        var footer = document.querySelector("#Footer");
        if (footer) footer.classList.add("hidden");

        // Load the iframe immediately — aircraft position is served by the FSTRaK tile server
        // directly from SimConnect data, so no SimVar access is needed here.
        if (this.iframeElement) {
            this.iframeElement.src = 'http://127.0.0.1:8765/panel';
        }

        if (this.ingameUi) {
            this.ingameUi.addEventListener("panelActive", function() {
                if (self.iframeElement && !self.iframeElement.src) {
                    self.iframeElement.src = 'http://127.0.0.1:8765/panel';
                }
            });
            this.ingameUi.addEventListener("panelInactive", function() {
                if (self.iframeElement) {
                    self.iframeElement.src = '';
                }
            });
        }
    }
    initialize() {}
    disconnectedCallback() {
        super.disconnectedCallback();
        if (this.iframeElement) {
            this.iframeElement.src = '';
        }
    }
}
window.customElements.define("ingamepanel-custom", IngamePanelCustomPanel);
checkAutoload();
