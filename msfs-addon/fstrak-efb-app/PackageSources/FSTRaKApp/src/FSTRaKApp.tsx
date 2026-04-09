import {
  App,
  AppBootMode,
  AppSuspendMode,
  AppView,
  AppViewProps,
  Efb,
  RequiredProps,
  TVNode,
} from '@efb/efb-api';

/**
 * Full-screen iframe view — loads the FSTRaK moving map from the tile server.
 * The tile server must be running (FSTRaK desktop app must be open).
 */
class FSTRaKAppView extends AppView<RequiredProps> {
  public render(): TVNode {
    return (
      <div class="fstrak-efb-container">
        <iframe
          src="http://127.0.0.1:8765/panel"
          class="fstrak-efb-iframe"
        />
      </div>
    ) as unknown as TVNode;
  }
}

/**
 * FSTRaK EFB app — registers the moving map as a tablet app in MSFS 2024.
 */
class FSTRaKApp extends App {
  public get name(): string {
    return 'FSTRaK';
  }

  public get icon(): string {
    return 'coui://html_ui/Icons/ICON_FSTRAK_EFB_APP.svg';
  }

  public override get bootMode(): AppBootMode {
    return AppBootMode.COLD;
  }

  public override get suspendMode(): AppSuspendMode {
    return AppSuspendMode.SLEEP;
  }

  public async install(): Promise<void> {
    await Efb.loadCss(
      'coui://html_ui/efb_ui/efb_apps/FSTRaKApp/FSTRaKApp.css'
    );
  }

  public render(props: AppViewProps): TVNode {
    return (<FSTRaKAppView {...props} />) as unknown as TVNode;
  }
}

Efb.use(FSTRaKApp);
