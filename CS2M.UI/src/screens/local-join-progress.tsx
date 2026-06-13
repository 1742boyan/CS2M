import { bindValue, trigger, useValue } from "cs2/api";
import { Button } from "cs2/ui";
import mod from "../../mod.json";
import { playerStatus$ } from "./player-joining-overlay";

export const downloadDone$ = bindValue<number>(mod.id, 'DownloadDone', 0);
export const downloadRemaining$ = bindValue<number>(mod.id, 'DownloadRemaining', 0);
export const downloadSpeed$ = bindValue<number>(mod.id, 'DownloadSpeed', 0);

function formatBytes(bytes: number, decimals = 2) {
    if (!+bytes) return '0 Bytes';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
}

export const LocalJoinProgress = () => {
    const playerStatus = useValue(playerStatus$);
    const downloadDone = useValue(downloadDone$);
    const downloadRemaining = useValue(downloadRemaining$);
    const downloadSpeed = useValue(downloadSpeed$);

    if (playerStatus !== 'WAITING_TO_JOIN' && 
        playerStatus !== 'DOWNLOADING_MAP' && 
        playerStatus !== 'LOADING_MAP') {
        return null;
    }

    const onCancel = () => {
        trigger(mod.id, "CancelJoin");
    };

    let statusText = "Connecting...";
    let details = null;
    let allowCancel = true;

    if (playerStatus === 'WAITING_TO_JOIN') {
        statusText = "Waiting for Host Approval...";
    } else if (playerStatus === 'DOWNLOADING_MAP') {
        statusText = "Downloading Map...";
        const total = downloadDone + downloadRemaining;
        const percentage = total > 0 ? ((downloadDone / total) * 100).toFixed(1) : "0.0";
        details = (
            <div style={{display: "flex", flexDirection: "column", gap: "5px", alignItems: "center"}}>
                <div style={{width: "300px", height: "10px", backgroundColor: "#333", borderRadius: "5px", overflow: "hidden"}}>
                    <div style={{width: `${percentage}%`, height: "100%", backgroundColor: "#4da6ff"}} />
                </div>
                <div style={{fontSize: "14px", color: "#bbb"}}>
                    {formatBytes(downloadDone)} / {formatBytes(total)} ({percentage}%)
                </div>
                <div style={{fontSize: "14px", color: "#bbb"}}>
                    Speed: {formatBytes(downloadSpeed)}/s
                </div>
            </div>
        );
    } else if (playerStatus === 'LOADING_MAP') {
        statusText = "Loading Map...";
        allowCancel = false; // Disable cancel during map load as it's unstable to kill load coroutine
    }

    return (
        <div style={{
            position: "absolute", top: 0, left: 0, right: 0, bottom: 0,
            backgroundColor: "rgba(0,0,0,0.8)", display: "flex", 
            justifyContent: "center", alignItems: "center", zIndex: 9999,
            pointerEvents: "auto"
        }}>
            <div style={{
                backgroundColor: "rgba(20,20,20,0.95)", border: "2px solid #555",
                borderRadius: "8px", padding: "30px", minWidth: "400px",
                display: "flex", flexDirection: "column", gap: "20px", alignItems: "center"
            }}>
                <div style={{fontSize: "24px", color: "white"}}>
                    {statusText}
                </div>
                
                {details}
                
                {allowCancel && (
                    <div style={{marginTop: "10px"}}>
                        <Button onSelect={onCancel}>Cancel</Button>
                    </div>
                )}
            </div>
        </div>
    );
};
