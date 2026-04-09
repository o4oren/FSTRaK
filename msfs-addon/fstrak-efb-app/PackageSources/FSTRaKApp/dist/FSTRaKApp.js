(() => {
  var __defProp = Object.defineProperty;
  var __getOwnPropSymbols = Object.getOwnPropertySymbols;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __propIsEnum = Object.prototype.propertyIsEnumerable;
  var __defNormalProp = (obj, key, value) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
  var __spreadValues = (a, b) => {
    for (var prop in b || (b = {}))
      if (__hasOwnProp.call(b, prop))
        __defNormalProp(a, prop, b[prop]);
    if (__getOwnPropSymbols)
      for (var prop of __getOwnPropSymbols(b)) {
        if (__propIsEnum.call(b, prop))
          __defNormalProp(a, prop, b[prop]);
      }
    return a;
  };
  var __require = /* @__PURE__ */ ((x) => typeof require !== "undefined" ? require : typeof Proxy !== "undefined" ? new Proxy(x, {
    get: (a, b) => (typeof require !== "undefined" ? require : a)[b]
  }) : x)(function(x) {
    if (typeof require !== "undefined") return require.apply(this, arguments);
    throw Error('Dynamic require of "' + x + '" is not supported');
  });

  // src/FSTRaKApp.tsx
  var import_efb_api = __require("@efb/efb-api");
  var FSTRaKAppView = class extends import_efb_api.AppView {
    render() {
      return /* @__PURE__ */ React.createElement("div", { class: "fstrak-efb-container" }, /* @__PURE__ */ React.createElement(
        "iframe",
        {
          src: "http://127.0.0.1:8765/panel",
          class: "fstrak-efb-iframe"
        }
      ));
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
      return /* @__PURE__ */ React.createElement(FSTRaKAppView, __spreadValues({}, props));
    }
  };
  import_efb_api.Efb.use(FSTRaKApp);
})();
