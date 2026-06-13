import React from 'react';
import { bindValue, useValue } from 'cs2/api';

const playerMouseDataBinding = bindValue<string>("CS2M", "PlayerMouseData", "[]");

export const PlayerMouseOverlay = () => {
    const mouseDataStr = useValue(playerMouseDataBinding);
    
    let players = [];
    try {
        players = JSON.parse(mouseDataStr as string);
    } catch (e) {
        // ignore
    }

    if (!players || players.length === 0) {
        return null;
    }

    return (
        <div style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: '100%', pointerEvents: 'none', zIndex: 1000 }}>
            {players.map((player: any) => (
                <div 
                    key={player.id} 
                    style={{
                        position: 'absolute',
                        left: player.x,
                        top: player.y,
                        transform: 'translate(-50%, -100%)',
                        backgroundColor: 'rgba(0, 0, 0, 0.6)',
                        color: 'white',
                        padding: '4px 8px',
                        borderRadius: '4px',
                        fontSize: '14px',
                        fontWeight: 'bold',
                        pointerEvents: 'none',
                        border: '2px solid rgba(255, 255, 255, 0.8)',
                        boxShadow: '0 2px 4px rgba(0,0,0,0.5)',
                        whiteSpace: 'nowrap',
                    }}
                >
                    <div style={{
                        position: 'absolute',
                        bottom: '-6px',
                        left: '50%',
                        transform: 'translateX(-50%)',
                        width: 0,
                        height: 0,
                        borderLeft: '6px solid transparent',
                        borderRight: '6px solid transparent',
                        borderTop: '6px solid rgba(255, 255, 255, 0.8)',
                    }}></div>
                    {player.name}
                </div>
            ))}
        </div>
    );
};
