import { bindValue, trigger, useValue } from "cs2/api";
import { LocalizedString, useLocalization } from "cs2/l10n";
import mod from "../../mod.json";
import { getModule } from "cs2/modding";
import { Button } from "cs2/ui";

export const showErrorDialog = bindValue<boolean>(mod.id, 'ShowErrorDialog', false);
export const joinErrorMessage = bindValue<Array<string>>(mod.id, 'JoinErrorMessage', []);

export const ErrorDialog = () => {
    const visible = useValue(showErrorDialog);
    const errMsg = useValue(joinErrorMessage);

    const {translate} = useLocalization();

    if (!visible) return null;

    let messages = <></>;
    let plainTextError = "";
    if (errMsg.length > 0) {
        messages = <span style={{color: "#ff8080"}}><LocalizedString id={"CS2M.UI.JoinError.Intro"}/></span>;
        plainTextError = translate("CS2M.UI.JoinError.Intro", "Error joining game:") + "\n";
    }

    for (let i = 0; i < errMsg.length; i++) {
        let err = errMsg[i];
        let message;
        if (err.startsWith("precondition:")) {
            const code = err.substring(13);
            switch (code) {
                case "GAME_VERSION_MISMATCH":
                case "MOD_VERSION_MISMATCH": {
                    const err_id = "CS2M.UI.JoinError." + code;
                    const serverVer = errMsg[++i];
                    const clientVer = errMsg[++i];
                    let translated = translate(err_id, err_id) || err_id;
                    translated = translated.replace("{SERVER}", serverVer).replace("{CLIENT}", clientVer);
                    plainTextError += "- " + translated + "\n";
                    message = <LocalizedString id={err_id} args={{SERVER: serverVer, CLIENT: clientVer}}/>;
                    break;
                }
                case "DLCS_MISMATCH":
                case "MODS_MISMATCH": {
                    const err_id = "CS2M.UI.JoinError." + code;
                    let translated = translate(err_id, err_id) || err_id;
                    const serverList = errMsg[++i];
                    const clientList = errMsg[++i];
                    
                    message = <LocalizedString id={err_id}/>;
                    if (serverList != '') {
                        const s_err_id = "CS2M.UI.JoinError." + code + ".server";
                        translated += " Server: " + serverList;
                        message = <>{message}<LocalizedString id={s_err_id} args={{SERVER: serverList}}/></>;
                    }
                    if (clientList != '') {
                        const c_err_id = "CS2M.UI.JoinError." + code + ".client";
                        translated += " Client: " + clientList;
                        message = <>{message}<LocalizedString id={c_err_id} args={{CLIENT: clientList}}/></>;
                    }
                    plainTextError += "- " + translated + "\n";
                    break;
                }
                case "USERNAME_NOT_AVAILABLE":
                case "PASSWORD_INCORRECT": {
                    const err_id = "CS2M.UI.JoinError." + code;
                    plainTextError += "- " + (translate(err_id, err_id) || err_id) + "\n";
                    message = <LocalizedString id={err_id}/>;
                    break;
                }
            }
        } else {
            plainTextError += "- " + (translate(err, err) || err) + "\n";
            message = <LocalizedString id={err}/>;
        }
        messages = <>{messages}<br/>{message}</>;
    }

    const onClose = () => trigger(mod.id, "CloseErrorDialog");
    const onCopy = () => {
        if (navigator.clipboard) {
            navigator.clipboard.writeText(plainTextError).catch(e => console.error(e));
        } else {
            console.warn("Clipboard API not available");
        }
    };
    const onOpenLogs = () => trigger(mod.id, "OpenLogsFolder");

    return (
        <div style={{
            position: "absolute", top: 0, left: 0, right: 0, bottom: 0,
            backgroundColor: "rgba(0,0,0,0.8)", display: "flex", 
            justifyContent: "center", alignItems: "center", zIndex: 9999
        }}>
            <div style={{
                backgroundColor: "rgba(20,20,20,0.95)", border: "2px solid #555",
                borderRadius: "8px", padding: "20px", maxWidth: "600px", minWidth: "400px",
                display: "flex", flexDirection: "column", gap: "20px"
            }}>
                <div style={{fontSize: "24px", color: "white", borderBottom: "1px solid #555", paddingBottom: "10px"}}>
                    <LocalizedString id="CS2M.UI.Error" fallback="Error Occurred"/>
                </div>
                
                <div style={{color: "white", fontSize: "16px", maxHeight: "300px", overflowY: "auto"}}>
                    {messages}
                </div>

                <div style={{display: "flex", justifyContent: "flex-end", gap: "10px", marginTop: "10px"}}>
                    <Button onSelect={onCopy}>Copy to Clipboard</Button>
                    <Button onSelect={onOpenLogs}>Open Logs</Button>
                    <Button onSelect={onClose}>Close</Button>
                </div>
            </div>
        </div>
    );
}
