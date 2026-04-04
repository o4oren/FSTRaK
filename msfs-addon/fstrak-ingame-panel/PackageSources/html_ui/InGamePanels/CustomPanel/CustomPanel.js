class IngamePanelCustomPanel extends TemplateElement {
    constructor() {
        super(...arguments);
        this.panelActive = false;
        this.started = false;
        this.ingameUi = null;
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
            });
            this.ingameUi.addEventListener("panelInactive", function(e) {
                self.panelActive = false;
                if (self.iframeElement) {
                    self.iframeElement.src = '';
                }
            });
        }
    }
    initialize() {
        if (this.started) return;
        this.started = true;
    }
    disconnectedCallback() {
        super.disconnectedCallback();
    }
}
window.customElements.define("ingamepanel-custom", IngamePanelCustomPanel);
checkAutoload();
