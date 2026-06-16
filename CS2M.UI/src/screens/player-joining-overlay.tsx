import { bindValue, trigger, useValue } from "cs2/api";
import { Button } from "cs2/ui";
import mod from "../../mod.json";

export const isPlayerJoining$ = bindValue<boolean>(mod.id, 'IsPlayerJoining', false);
export const joiningUsername$ = bindValue<string>(mod.id, 'JoiningUsername', '');
export const queueLength$ = bindValue<number>(mod.id, 'QueueLength', 0);
export const playerStatus$ = bindValue<string>(mod.id, 'PlayerStatus', 'INACTIVE'); // To determine if we are HOST
export const isHost$ = bindValue<boolean>(mod.id, 'IsHost', false);

export const PlayerJoiningOverlay = () => {
    const isPlayerJoining = useValue(isPlayerJoining$);
    const joiningUsername = useValue(joiningUsername$);
    const queueLength = useValue(queueLength$);
    const isHost = useValue(isHost$);

    if (!isPlayerJoining || isHost) return null;

    const onKick = () => {
        trigger(mod.id, "KickJoiningPlayer");
    };

    return (
        <div 
            onPointerDown={(e) => e.stopPropagation()}
            onPointerUp={(e) => e.stopPropagation()}
            onPointerMove={(e) => e.stopPropagation()}
            onWheel={(e) => e.stopPropagation()}
            style={{
                position: "absolute", top: 0, left: 0, right: 0, bottom: 0,
                backgroundColor: "rgba(0,0,0,0.8)", display: "flex", 
                justifyContent: "center", alignItems: "center", zIndex: 9998,
                pointerEvents: "auto"
            }}
        >
            <div style={{
                backgroundColor: "rgba(20,20,20,0.95)", border: "2px solid #555",
                borderRadius: "8px", padding: "40px", minWidth: "500px",
                display: "flex", flexDirection: "column", gap: "20px", alignItems: "center"
            }}>
                <div style={{fontSize: "26px", color: "white", textAlign: "center"}}>
                    Player <span style={{color: "#4da6ff", fontWeight: "bold"}}>{joiningUsername}</span> is joining...
                </div>
                
                <div style={{fontSize: "18px", color: "#ccc", textAlign: "center", marginBottom: "10px"}}>
                    Waiting for host's approval. The game is currently paused.
                </div>

                {queueLength > 0 && (
                    <div style={{fontSize: "16px", color: "#aaa"}}>
                        Players waiting in queue: {queueLength}
                    </div>
                )}
                
                <div style={{marginTop: "20px"}}>
                    <Button onSelect={onKick} style={{padding: "8px 20px", backgroundColor: "#c0392b", color: "white"}}>Kick Player</Button>
                </div>
            </div>
        </div>
    );
};
