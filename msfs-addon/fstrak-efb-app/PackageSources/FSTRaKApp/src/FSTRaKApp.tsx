import { FSComponent, VNode } from '@microsoft/msfs-sdk';
import {
  App,
  AppBootMode,
  AppInstallProps,
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
class FSTRaKAppView extends AppView<RequiredProps<AppViewProps, 'bus'>> {
  protected defaultView = 'MapView';

  protected registerViews(): void {
    this.appViewService.registerPage('MapView', () => (
      <div class="fstrak-efb-container">
        <iframe src="http://127.0.0.1:8765/panel" class="fstrak-efb-iframe" />
      </div>
    ));
  }

  public render(): VNode {
    return <div class="fstrak-efb-root">{super.render()}</div>;
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

  public BootMode = AppBootMode.COLD;
  public SuspendMode = AppSuspendMode.SLEEP;

  public async install(_props: AppInstallProps): Promise<void> {
    Efb.loadCss('coui://html_ui/efb_ui/efb_apps/FSTRaKApp/FSTRaKApp.css');
    return Promise.resolve();
  }

  public render(): TVNode<FSTRaKAppView> {
    return <FSTRaKAppView bus={this.bus} />;
  }
}

Efb.use(FSTRaKApp);
