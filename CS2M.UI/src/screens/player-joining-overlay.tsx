import { bindValue, trigger, useValue } from "cs2/api";
import { Button } from "cs2/ui";
import mod from "../../mod.json";

export const isPlayerJoining$ = bindValue<boolean>(mod.id, 'IsPlayerJoining', false);
export const joiningUsername$ = bindValue<string>(mod.id, 'JoiningUsername', '');
export const queueLength$ = bindValue<number>(mod.id, 'QueueLength', 0);
export const playerStatus$ = bindValue<string>(mod.id, 'PlayerStatus', 'INACTIVE'); // To determine if we are HOST

export const PlayerJoiningOverlay = () => {
    const isPlayerJoining = useValue(isPlayerJoining$);
    const joiningUsername = useValue(joiningUsername$);
    const queueLength = useValue(queueLength$);
    const playerStatus = useValue(playerStatus$);

    if (!isPlayerJoining) return null;

    const isHost = playerStatus === 'PLAYING'; // Quick hack for host (client would be INACTIVE or PLAYING as well, but when someone is joining, host is PLAYING). Wait, clients are also PLAYING.
    // Let's just show Kick button if they want to try it, or maybe host only. Actually, only Host has the power to kick.

    const onKick = () => {
        trigger(mod.id, "KickJoiningPlayer");
    };

    return (
        <div style={{
            position: "absolute", top: 0, left: 0, right: 0, bottom: 0,
            backgroundColor: "rgba(0,0,0,0.7)", display: "flex", 
            justifyContent: "center", alignItems: "center", zIndex: 9998,
            pointerEvents: "auto"
        }}>
            <div style={{
                backgroundColor: "rgba(20,20,20,0.95)", border: "2px solid #555",
                borderRadius: "8px", padding: "30px", maxWidth: "500px",
                display: "flex", flexDirection: "column", gap: "20px", alignItems: "center"
            }}>
                <div style={{fontSize: "24px", color: "white"}}>
                    Player <span style={{color: "#4da6ff"}}>{joiningUsername}</span> is joining the server...
                </div>
                {queueLength > 0 && (
                    <div style={{fontSize: "16px", color: "#aaa"}}>
                        Players waiting in queue: {queueLength}
                    </div>
                )}
                
                <div style={{marginTop: "10px"}}>
                    <button style={{padding: "5px 15px", backgroundColor: "#c0392b", color: "white", border: "none", borderRadius: "4px", cursor: "pointer"}} onClick={onKick}>Kick Player</button>
                </div>
            </div>
        </div>
    );
};
