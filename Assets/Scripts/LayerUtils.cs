using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LayerUtils {
    public static int PlayerOrEnemyLayermask = (
     (1 << LayerMask.NameToLayer("Player"))
     | (1 << LayerMask.NameToLayer("Enemy"))
    );
    public static int NonPlayerOrEnemyLayermask = ~PlayerOrEnemyLayermask;

    public static int PlayerLayermask = (
        (1 << LayerMask.NameToLayer("Player"))
    );
    public static int NonPlayerLayermask = ~PlayerLayermask;

    public static int GroundLayermask = (
     NonPlayerOrEnemyLayermask
    );

    public static int EnemyLayermask = (
        (1 << LayerMask.NameToLayer("Enemy"))
    );

    public static int ShipLayermask = (
        (1 << LayerMask.NameToLayer("Ship"))
    );
}
