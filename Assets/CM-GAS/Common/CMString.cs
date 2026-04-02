using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CMString
{
    private string _value;
    public string value { get { return _value; } private set { } }

    public CMString()
    {
        this._value = "";
    }

    public CMString(string str)
    {
        this._value = str;
    }

    public static implicit operator CMString(string value)
    {
        return new CMString(value);
    }

    public static explicit operator string(CMString CMString)
    {
        return CMString.value;
    }

    public override string ToString()
    {
        return value;
    }
}
