﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Target : MonoBehaviour
{
    public HealthController healthController;

    void Start()
    {
        healthController = gameObject.GetComponent<HealthController>();
        healthController.OnDamaged += HealthController_OnDamaged;
        healthController.OnDeath += HealthController_OnDeath;
    }

    void HealthController_OnDamaged()
    {
        gameObject.GetComponent<Animator>().Play("Damaged", -1, 0f);
    }

    void HealthController_OnDeath()
    {
        gameObject.GetComponent<Animator>().Play("Dead");
    }
}
