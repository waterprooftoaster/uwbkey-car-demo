using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using TMPro;
using Unity.VisualScripting;
//using UnityEditor.SceneTemplate;
using UnityEngine;
using UnityEngine.UI;

public class PersonMovement : MonoBehaviour
{
    GameObject t1;
    GameObject t2;
    GameObject t3;
    GameObject RectArea;
    GameObject TriArea;
    SpriteRenderer Render;
    //GameObject door;
    //Vector3 origin;
    GameObject carParent;
    GameObject threshold_1;
    GameObject threshold_2;
    GameObject threshold_3;
    Transform inside;
    Transform unlock;
    Transform welcome;
    Transform inside_in;
    Transform unlock_in;
    Transform welcome_in;

    GameObject inside_t_slider;
    GameObject unlock_t_slider;
    GameObject welcome_t_slider;
    Transform slider1;
    Transform slider2;
    Transform slider3;
    Transform slider_text1;
    Transform slider_text2;
    Transform slider_text3;

    GameObject CarBody;
    GameObject tirefl;
    GameObject tirefr;
    GameObject tirerl;
    GameObject tirerr;
    GameObject rimfl;
    GameObject rimfr;
    GameObject rimrl;
    GameObject rimrr;
    List<UnityEngine.Color> oldColor = new List<UnityEngine.Color>();
    List<UnityEngine.Color> newColor = new List<UnityEngine.Color>();
    UnityEngine.Color oldColor1;
    UnityEngine.Color newColor1;
    UnityEngine.Color oldColor2;
    UnityEngine.Color newColor2;
    bool transparent = false;

    Vector3 anchor_1;
    GameObject p;

    GameObject update_val;
    GameObject delay_val;
    GameObject update_label;
    GameObject delay_label;
    GameObject update_val_text;
    GameObject delay_val_text;

    bool ConstantTransparentCar = false;

    // Helper Class file attached to person gameObject to help with detecting movement/collisions invoving the person and modify rotation; not used to actually move the person

    void Start()
    {
        //get objects instantitated
        t1 = GameObject.Find("Point1");
        t2 = GameObject.Find("Point2");
        t3 = GameObject.Find("Point3");

        carParent = GameObject.Find("sedan-car-01");
        threshold_1 = GameObject.Find("InsideThreshold");
        threshold_2 = GameObject.Find("UnlockThreshold");
        threshold_3 = GameObject.Find("WelcomeThreshold");

        inside = threshold_1.transform.GetChild(0);
        unlock = threshold_2.transform.GetChild(0);
        welcome = threshold_3.transform.GetChild(0);
        inside_in = threshold_1.transform.GetChild(1);
        unlock_in = threshold_2.transform.GetChild(1);
        welcome_in = threshold_3.transform.GetChild(1);

        inside_t_slider = GameObject.Find("InsideThresholdSlider");
        unlock_t_slider = GameObject.Find("UnlockThresholdSlider");
        welcome_t_slider = GameObject.Find("WelcomeThresholdSlider");
        slider1 = inside_t_slider.transform.GetChild(1);
        slider2 = unlock_t_slider.transform.GetChild(1);
        slider3 = welcome_t_slider.transform.GetChild(1);
        slider_text1 = inside_t_slider.transform.GetChild(2);
        slider_text2 = unlock_t_slider.transform.GetChild(2);
        slider_text3 = welcome_t_slider.transform.GetChild(2);

        update_val = GameObject.Find("UpdateThresholdSlider");
        delay_val = GameObject.Find("DelaySlider");
        update_label = GameObject.Find("UpdateLabel");
        delay_label = GameObject.Find("DelayLabel");
        update_val_text = GameObject.Find("UpdateValText");
        delay_val_text = GameObject.Find("DelayValText");

        anchor_1 = GameObject.Find("Anchor1").transform.position;
        p = GameObject.Find("Person");
        //anchor_1.x = p.transform.position.x;
        //anchor_1.z = p.transform.position.z;
        anchor_1.y = p.transform.position.y;

        CarBody = GameObject.Find("body");
        tirefl = GameObject.Find("tire1-fl");
        tirefr = GameObject.Find("tire1-fr");
        tirerl = GameObject.Find("tire1-rl");
        tirerr = GameObject.Find("tire1-rr");
        rimfl = GameObject.Find("rim-fl");
        rimfr = GameObject.Find("rim-fr");
        rimrl = GameObject.Find("rim-rl");
        rimrr = GameObject.Find("rim-rr");

        float trans = 0.1f;
        int tempi = 0;
        foreach (var mat in CarBody.GetComponent<MeshRenderer>().materials)
        {
            oldColor.Add(mat.color);
            newColor.Add(new UnityEngine.Color(oldColor[tempi].r, oldColor[tempi].g, oldColor[tempi].b, trans));
            tempi++;
        }
        oldColor1 = tirefl.GetComponent<MeshRenderer>().material.color;
        newColor1 = new UnityEngine.Color(oldColor1.r, oldColor1.g, oldColor1.b, trans);
        oldColor2 = rimfl.GetComponent<MeshRenderer>().material.color;
        newColor2 = new UnityEngine.Color(oldColor2.r, oldColor2.g, oldColor2.b, trans);
    }

    // Update is called once per frame
    void Update()
    {
        //make person look at middle anchor
        p.transform.LookAt(anchor_1);

        // update slider text
        slider_text1.GetComponent<TMP_Text>().text = slider1.GetComponent<Slider>().value.ToString("F2") + "M";
        slider_text2.GetComponent<TMP_Text>().text = slider2.GetComponent<Slider>().value.ToString("F2") + "M";
        slider_text3.GetComponent<TMP_Text>().text = slider3.GetComponent<Slider>().value.ToString("F2") + "M";
        update_val_text.GetComponent<TMP_Text>().text = update_val.GetComponent<Slider>().value.ToString("F2");
        delay_val_text.GetComponent<TMP_Text>().text = delay_val.GetComponent<Slider>().value.ToString("F2");

        //Change color if detected in area
        //  FF8686 light red
        //  86FF89 light green
        Boolean w = false;
        Boolean u = false;
        Boolean i = false;

        //check if person is inside each threshold and highlight threshold in green if so
        
        if (!ConstantTransparentCar)
        {
            //inside
            if (inside.GetComponent<CapsuleCollider>().bounds.Contains(transform.position))
            {
                i = true;
                //print("inside of inside car threshold");
                UnityEngine.Color color;
                if (UnityEngine.ColorUtility.TryParseHtmlString("#86FF89", out color))
                {
                    Render = inside.GetComponent<SpriteRenderer>();
                    Render.color = color;
                }
                CarTransparency(true);
            }
            else
            {
                UnityEngine.Color color;
                if (UnityEngine.ColorUtility.TryParseHtmlString("#FF8686", out color))
                {
                    Render = inside.GetComponent<SpriteRenderer>();
                    Render.color = color;
                }
                CarTransparency(false);
            }
            // unlock
            if (unlock.GetComponent<CapsuleCollider>().bounds.Contains(transform.position) && !i)
            {
                u = true;
                //print("inside of welcome threshold");
                UnityEngine.Color color;
                if (UnityEngine.ColorUtility.TryParseHtmlString("#86FF89", out color))
                {
                    Render = unlock.GetComponent<SpriteRenderer>();
                    Render.color = color;
                }
            }
            else
            {
                UnityEngine.Color color;
                if (UnityEngine.ColorUtility.TryParseHtmlString("#FF8686", out color))
                {
                    Render = unlock.GetComponent<SpriteRenderer>();
                    Render.color = color;
                }
            }
            //welcome
            if (welcome.GetComponent<CapsuleCollider>().bounds.Contains(transform.position) && !i && !u)
            {
                w = true;
                //print("inside of welcome threshold");
                UnityEngine.Color color;
                if (UnityEngine.ColorUtility.TryParseHtmlString("#86FF89", out color))
                {
                    Render = welcome.GetComponent<SpriteRenderer>();
                    Render.color = color;
                }
            }
            else
            {
                UnityEngine.Color color;
                if (UnityEngine.ColorUtility.TryParseHtmlString("#FF8686", out color))
                {
                    Render = welcome.GetComponent<SpriteRenderer>();
                    Render.color = color;
                }
            }
        }

    }

    public void move(float newX, float newY, float newZ)
    {
        transform.position = new Vector3(newX, newY, newZ);
    }

    /* Returns true if point p lies inside triangle a-b-c */
    Boolean PointInTri(Vector3 t1, Vector3 t2, Vector3 t3, Vector3 pos)
    {
        // create temporary points to convert from V3 to V2 by replacing y with z
        Vector3 temp1 = t1;
        Vector3 temp2 = t2;
        Vector3 temp3 = t3;
        Vector3 temp_pos = pos;

        temp1.y = temp1.z;
        temp2.y = temp2.z;
        temp3.y = temp3.z;
        temp_pos.y = temp_pos.z;

        Vector2 a = temp1;
        Vector2 b = temp2;
        Vector2 c = temp3;
        Vector2 p = temp_pos;

        // calculate if inside
        Vector2 v0 = b - c;
        Vector2 v1 = a - c;
        Vector2 v2 = p - c;
        float dot00 = Vector2.Dot(v0, v0);
        float dot01 = Vector2.Dot(v0, v1);
        float dot02 = Vector2.Dot(v0, v2);
        float dot11 = Vector2.Dot(v1, v1);
        float dot12 = Vector2.Dot(v1, v2);
        float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;
        return (u > 0.0f) && (v > 0.0f) && (u + v < 1.0f);
    }

    // updates inside threshold
    public void update_threshold1()
    {
        float val = slider1.GetComponent<Slider>().value * 2;
        Vector3 temp = new Vector3(val, val);
        Vector3 temp2 = new Vector3((float)(val-0.2), (float)(val-0.2));
        //threshold_1.transform.localScale = temp;
        inside.localScale = temp;
        inside_in.localScale = temp2;
    }
    //updates unlock threshold
    public void update_threshold2()
    {
        float val = slider2.GetComponent<Slider>().value*2;
        Vector3 temp = new Vector3(val, val);
        Vector3 temp2 = new Vector3((float)(val - 0.2), (float)(val - 0.2));
        //threshold_1.transform.localScale = temp;
        unlock.localScale = temp;
        unlock_in.localScale = temp2;
    }
    //updates welcome threshold
    public void update_threshold3()
    {
        float val = slider3.GetComponent<Slider>().value*2;
        Vector3 temp = new Vector3(val, val);
        Vector3 temp2 = new Vector3((float)(val - 0.2), (float)(val - 0.2));
        //threshold_1.transform.localScale = temp;
        welcome.localScale = temp;
        welcome_in.localScale = temp2;
    }

    public void toggleTransparent(bool toggle)
    {
        if (toggle)
        {
            CarTransparency(true);
            ConstantTransparentCar = true;
        }
        else
        {
            CarTransparency(false);
            ConstantTransparentCar = false;
        }
    }

    // makes care transparent or opaque; only make transparent when person is within inside threshold
    public void CarTransparency(bool make_transparent)
    {

        int i = 0;
        if (!transparent && make_transparent)
        {
            // car body
            foreach (var mat in CarBody.GetComponent<MeshRenderer>().materials)
            {
                mat.SetColor("_Color", newColor[i]);
                i++;
            }

            //tires
            tirefl.GetComponent<MeshRenderer>().material.SetColor("_Color", newColor1);
            tirefr.GetComponent<MeshRenderer>().material.SetColor("_Color", newColor1);
            tirerl.GetComponent<MeshRenderer>().material.SetColor("_Color", newColor1);
            tirerr.GetComponent<MeshRenderer>().material.SetColor("_Color", newColor1);

            //rim
            rimfl.GetComponent<MeshRenderer>().material.SetColor("_Color", newColor2);
            rimfr.GetComponent<MeshRenderer>().material.SetColor("_Color", newColor2);
            rimrl.GetComponent<MeshRenderer>().material.SetColor("_Color", newColor2);
            rimrr.GetComponent<MeshRenderer>().material.SetColor("_Color", newColor2);

            transparent = true;
        }
        else if (transparent && !make_transparent)
        {
            // car body
            foreach (var mat in CarBody.GetComponent<MeshRenderer>().materials)
            {
                mat.SetColor("_Color", oldColor[i]);
                i++;
            }

            //tires
            tirefl.GetComponent<MeshRenderer>().material.SetColor("_Color", oldColor1);
            tirefr.GetComponent<MeshRenderer>().material.SetColor("_Color", oldColor1);
            tirerl.GetComponent<MeshRenderer>().material.SetColor("_Color", oldColor1);
            tirerr.GetComponent<MeshRenderer>().material.SetColor("_Color", oldColor1);

            //rim
            rimfl.GetComponent<MeshRenderer>().material.SetColor("_Color", oldColor2);
            rimfr.GetComponent<MeshRenderer>().material.SetColor("_Color", oldColor2);
            rimrl.GetComponent<MeshRenderer>().material.SetColor("_Color", oldColor2);
            rimrr.GetComponent<MeshRenderer>().material.SetColor("_Color", oldColor2);

            transparent = false;
        }
    }
}
