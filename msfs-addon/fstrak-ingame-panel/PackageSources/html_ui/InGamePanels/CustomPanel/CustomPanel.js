class IngamePanelCustomPanel extends TemplateElement {
    constructor() {
        super(...arguments);
        this.panelActive = false;
        this.started = false;
        this.ingameUi = null;
        this.debugEnabled = false;
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
            this.ingameUi.addEventListener("panelActive", (e) => {
                self.panelActive = true;
                if (self.iframeElement) {
                    self.iframeElement.src = 'http://localhost:8765/panel';
                }
            });
            this.ingameUi.addEventListener("panelInactive", (e) => {
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
