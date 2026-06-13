import { bindValue, trigger, useValue } from "cs2/api";
import { Button } from "cs2/ui";
import mod from "../../mod.json";

export const joinQueue$ = bindValue<Array<string>>(mod.id, 'JoinQueue', []);

export const HostQueueManager = () => {
    const joinQueue = useValue(joinQueue$);

    if (joinQueue.length === 0) return null;

    return (
        <div 
            onPointerDown={(e) => e.stopPropagation()}
            onPointerUp={(e) => e.stopPropagation()}
            onPointerMove={(e) => e.stopPropagation()}
            onWheel={(e) => e.stopPropagation()}
            style={{
                position: "absolute", top: 0, left: 0, right: 0, bottom: 0,
                backgroundColor: "rgba(0,0,0,0.8)", display: "flex", 
                justifyContent: "center", alignItems: "center", zIndex: 9999,
                pointerEvents: "auto"
            }}
        >
            <div style={{
                backgroundColor: "rgba(20,20,20,0.95)", border: "2px solid #555",
                borderRadius: "8px", padding: "30px", minWidth: "400px",
                display: "flex", flexDirection: "column", gap: "20px"
            }}>
                <div style={{fontSize: "24px", color: "white", borderBottom: "1px solid #555", paddingBottom: "10px", textAlign: "center"}}>
                    Player Join Requests ({joinQueue.length})
                </div>
                
                <div style={{maxHeight: "300px", overflowY: "auto", display: "flex", flexDirection: "column", gap: "10px"}}>
                    {joinQueue.map((item, index) => {
                        const parts = item.split(':');
                        const peerId = parseInt(parts[0], 10);
                        const username = parts[1];

                        return (
                            <div key={index} style={{
                                display: "flex", justifyContent: "space-between", alignItems: "center",
                                backgroundColor: "rgba(255,255,255,0.1)", padding: "10px 15px", borderRadius: "6px",
                                fontSize: "18px"
                            }}>
                                <span style={{color: "white"}}>{username}</span>
                                <div style={{display: "flex", gap: "10px"}}>
                                    <Button onSelect={() => trigger(mod.id, "ApproveJoin", parts[0])} style={{padding: "5px 15px", backgroundColor: "#27ae60", color: "white"}}>Approve</Button>
                                    <Button onSelect={() => trigger(mod.id, "DenyJoin", parts[0])} style={{padding: "5px 15px", backgroundColor: "#c0392b", color: "white"}}>Deny</Button>
                                </div>
                            </div>
                        );
                    })}
                </div>
            </div>
        </div>
    );
};
