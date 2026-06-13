import {getModule, ModRegistrar} from "cs2/modding";
import {MenuUIExtensions, PauseMenuCSMExtend} from "extends/main-menu";
import {GlobalOverlaysExtend} from "extends/global-overlays";
import {JoinGameMenu} from "./screens/join-game-menu";
import {HostGameMenu} from "./screens/host-game-menu";
import {ChatIcon, ChatPanel} from "./screens/chat";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.extend('game-ui/common/input/button/labeled-icon-button.tsx', 'LabeledIconButton', PauseMenuCSMExtend);
    moduleRegistry.add('cs2m/screens/join-game-menu.tsx', JoinGameMenu);
    moduleRegistry.add('cs2m/screens/host-game-menu.tsx', HostGameMenu);

    moduleRegistry.extend('game-ui/common/animations/transition-group-coordinator.tsx', 'TransitionGroupCoordinator', MenuUIExtensions);
    
    moduleRegistry.extend('game-ui/game/game.tsx', 'Game', GlobalOverlaysExtend);
    moduleRegistry.extend('game-ui/menu/menu.tsx', 'Menu', GlobalOverlaysExtend);

    moduleRegistry.append('GameBottomRight', ChatIcon);
    getModule('game-ui/game/components/game-panel-renderer.tsx', 'gamePanelComponents')['CS2M.UI.ChatPanel'] = ChatPanel;
}

export default register;
