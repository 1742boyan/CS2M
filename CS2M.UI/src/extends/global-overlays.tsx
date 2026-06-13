import {ModuleRegistryExtend} from "cs2/modding";
import {PlayerJoiningOverlay} from "../screens/player-joining-overlay";
import {HostQueueManager} from "../screens/host-queue-manager";
import {LocalJoinProgress} from "../screens/local-join-progress";

export const GlobalOverlaysExtend: ModuleRegistryExtend = (Component) => {
    return (props) => {
        return (
            <>
                <Component {...props} />
                <PlayerJoiningOverlay />
                <HostQueueManager />
                <LocalJoinProgress />
            </>
        );
    };
}
