import { bindValue, trigger, useValue } from "cs2/api";
import { Button } from "cs2/ui";
import mod from "../../mod.json";

export const joinQueue$ = bindValue<Array<string>>(mod.id, 'JoinQueue', []);
export const autoApproveJoins$ = bindValue<boolean>(mod.id, 'AutoApproveJoins', true);

export const HostQueueManager = () => {
    const joinQueue = useValue(joinQueue$);
    const autoApproveJoins = useValue(autoApproveJoins$);

    if (joinQueue.length === 0) return null;

    const onToggleAutoApprove = () => {
        trigger(mod.id, "SetAutoApproveJoins", !autoApproveJoins);
    };

    return (
        <div style={{
            position: "absolute", top: "20px", right: "20px",
            backgroundColor: "rgba(20,20,20,0.9)", border: "1px solid #555",
            borderRadius: "8px", padding: "15px", minWidth: "300px",
            zIndex: 9995, pointerEvents: "auto", display: "flex", flexDirection: "column", gap: "10px"
        }}>
            <div style={{fontSize: "18px", color: "white", borderBottom: "1px solid #555", paddingBottom: "5px"}}>
                Join Requests ({joinQueue.length})
            </div>
            
            <div style={{display: "flex", alignItems: "center", gap: "10px", marginTop: "5px", marginBottom: "5px"}}>
                <input 
                    type="checkbox" 
                    checked={autoApproveJoins} 
                    onChange={onToggleAutoApprove} 
                    id="autoApproveToggle"
                />
                <label htmlFor="autoApproveToggle" style={{color: "white"}}>Auto-Approve Joins</label>
            </div>

            <div style={{maxHeight: "200px", overflowY: "auto", display: "flex", flexDirection: "column", gap: "5px"}}>
                {joinQueue.map((item, index) => {
                    const parts = item.split(':');
                    const peerId = parseInt(parts[0], 10);
                    const username = parts[1];

                    return (
                        <div key={index} style={{
                            display: "flex", justifyContent: "space-between", alignItems: "center",
                            backgroundColor: "rgba(255,255,255,0.1)", padding: "5px 10px", borderRadius: "4px"
                        }}>
                            <span style={{color: "white"}}>{username}</span>
                            <div style={{display: "flex", gap: "5px"}}>
                                <Button onSelect={() => trigger(mod.id, "ApproveJoin", peerId)} style={{padding: "2px 8px", minHeight: "unset", minWidth: "unset"}}>Approve</Button>
                                <Button onSelect={() => trigger(mod.id, "DenyJoin", peerId)} style={{padding: "2px 8px", minHeight: "unset", minWidth: "unset"}}>Deny</Button>
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
};
