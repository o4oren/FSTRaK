(() => {
  var __create = Object.create;
  var __defProp = Object.defineProperty;
  var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __getProtoOf = Object.getPrototypeOf;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __commonJS = (cb, mod) => function __require() {
    return mod || (0, cb[__getOwnPropNames(cb)[0]])((mod = { exports: {} }).exports, mod), mod.exports;
  };
  var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
      for (let key of __getOwnPropNames(from))
        if (!__hasOwnProp.call(to, key) && key !== except)
          __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
  };
  var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
    // If the importer is in node compatibility mode or this is not an ESM
    // file that has been converted to a CommonJS file using a Babel-
    // compatible transform (i.e. "__esModule" has not been set), then set
    // "default" to the CommonJS "module.exports" for node compatibility.
    isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
    mod
  ));

  // global-externals:@efb/efb-api
  var require_efb_api = __commonJS({
    "global-externals:@efb/efb-api"(exports, module) {
      module.exports = EFB_API;
    }
  });

  // src/FSTRaKApp.tsx
  var import_efb_api = __toESM(require_efb_api());
  var FSTRaKAppView = class extends import_efb_api.AppView {
    render() {
      return FSComponent.buildComponent(
        "div",
        { class: "fstrak-efb-container" },
        FSComponent.buildComponent("iframe", {
          src: "http://127.0.0.1:8765/panel",
          class: "fstrak-efb-iframe"
        })
      );
    }
  };
  var FSTRaKApp = class extends import_efb_api.App {
    get name() {
      return "FSTRaK";
    }
    get icon() {
      return "coui://html_ui/Icons/ICON_FSTRAK_EFB_APP.svg";
    }
    get bootMode() {
      return import_efb_api.AppBootMode.COLD;
    }
    get suspendMode() {
      return import_efb_api.AppSuspendMode.SLEEP;
    }
    async install() {
      await import_efb_api.Efb.loadCss(
        "coui://html_ui/efb_ui/efb_apps/FSTRaKApp/FSTRaKApp.css"
      );
    }
    render(props) {
      return new FSTRaKAppView(props);
    }
  };
  import_efb_api.Efb.use(FSTRaKApp);
})();
