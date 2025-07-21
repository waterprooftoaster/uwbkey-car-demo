using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using TMPro;
using Unity.VisualScripting;

//using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

//using static UnityEditor.Experimental.GraphView.GraphView;

// class file attached to gameObject within scene to manage all serial processes and moving the person gameObject / updating UI

public class CARSerialPortReader : MonoBehaviour
{
    SerialPort serial = new SerialPort();
    public int num_bytes = 50;
    TMP_Dropdown dropdown;

    GameObject person;
    GameObject car;
    float car_center_x;
    float car_len;
    float car_width;
    GameObject inner_rect;

    byte[] data;

    //object array of anchor objects
    GameObject[] anchor;
    //object arrat of entry text boxes for anchor config
    GameObject[] anchor_input;
    //coordinate positions of anchors
    position_t[] anchor_positions;
    //arrays of each axis points 
    int[] m_AnchorPositionXAxis;
    int[] m_AnchorPositionYAxis;
    int[] m_AnchorPositionZAxis;
    //object array of anchor text label on the right side of UI
    GameObject[] anchor_text;

    // CAR DATA 
    int num_anchors = 6; // set to 6 even though there is 5

    public const int SUCCESS = 1;
    public const int INVALID = 0;

    //double scale = 1;
    double proportion = 0.01; // for converting distances (cm) to meter (m); change this to change scale
    float new_scale_len = 0.01F; // for use to convert sizes to smaller sizes for view
    float new_scale_wid = 0.5F;

    //for key position
    GameObject key_text1;
    GameObject key_text2;

    //for displaying anchor config write messages
    GameObject announcement_text;

    const int UWB_ANCHOR_INSIDE = 0;
    const int UWB_ANCHOR_RL = 1;
    const int UWB_ANCHOR_RR = 2;
    const int UWB_ANCHOR_FR = 3;
    const int UWB_ANCHOR_FL = 4;
    const int UWB_ANCHOR_INSIDE2 = 5;

    //threshold sliders
    Transform slider1;
    Transform slider2;
    Transform slider3;

    //for movement smoothening and outlier removal
    List<Vector3> MoveVectors = new List<Vector3>();
    List<Vector3> OutlierBuffer = new List<Vector3>();
    GameObject update_val;
    GameObject delay_val;
    TMPro.TMP_InputField outlier_limit;

    //three camera perspectives
    GameObject cam1;
    GameObject cam2;
    GameObject cam3;

    // structs for use saving positions / anchor info
    public struct Point
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
    //struct used in position calculations
    public struct uwbinfo_t
    {
        public double[] anchorsMeasuredDistance; // in meters
        public uint[] rangingStatus; // 1 = success, 0 = error
        public Int32[] anchorPositionsX; // in mm
        public Int32[] anchorPositionsY;
        public Int32[] anchorPositionsZ;

        public uwbinfo_t(int size)
        {
            anchorsMeasuredDistance = new double[size]; // in meters
            rangingStatus = new uint[size]; // 1 = success, 0 = error
            anchorPositionsX = new Int32[size]; // in mm
            anchorPositionsY = new Int32[size];
            anchorPositionsZ = new Int32[size];
        }
    }

    // struct for use saving positions / anchor info
    public struct position_t
    {
        public Int32 x_pos_cm;
        public Int32 y_pos_cm;
        public Int32 z_pos_cm;

        public position_t(Int32 x, Int32 y, Int32 z)
        {
            x_pos_cm = x;
            y_pos_cm = y;
            z_pos_cm = z;
        }
    }

    void Start()
    {
        //set serial settings
        serial.ReadTimeout = 1000;
        serial.BaudRate = 115200;
        serial.DataBits = 8; 
        serial.StopBits = StopBits.Two;
        serial.Parity = Parity.None;

        // Set game objects
        dropdown = GameObject.Find("SerialPortDropdown").GetComponent<TMP_Dropdown>();
        person = GameObject.Find("Person");
        car = GameObject.Find("Car");
        slider1 = GameObject.Find("InsideThresholdSlider").transform.GetChild(1);
        slider2 = GameObject.Find("UnlockThresholdSlider").transform.GetChild(1);
        slider3 = GameObject.Find("WelcomeThresholdSlider").transform.GetChild(1);

        //instantiate anchors
        m_AnchorPositionXAxis = new int[num_anchors];
        m_AnchorPositionYAxis = new int[num_anchors];
        m_AnchorPositionZAxis = new int[num_anchors];
        anchor = new GameObject[num_anchors];
        anchor_input = new GameObject[num_anchors];
        anchor_text = new GameObject[num_anchors];
        for (int i = 0; i < num_anchors; i++)
        {
            anchor[i] = GameObject.Find("Anchor" + (i + 1));
            anchor_input[i] = GameObject.Find("anchor" + (i + 1) + "_panel");
            anchor_text[i] = GameObject.Find("AnchorLabel" + (i + 1));
        }

        //cameras
        cam1 = GameObject.Find("Main Camera 1");
        cam2 = GameObject.Find("Side Camera 2");
        cam3 = GameObject.Find("Overhead 3");

        // config buttons
        update_val = GameObject.Find("UpdateThresholdSlider");
        delay_val = GameObject.Find("DelaySlider");
        outlier_limit = GameObject.Find("OutlierLimitField").GetComponent<TMP_InputField>();
        GameObject.Find("config_panel").SetActive(false);                                    
        //GameObject.Find("config_button_panel").SetActive(false); // not needed anymore

        getInitialScales();

        // set initial anchor positions
        // if no config avaialble, set to default
        anchor_positions = new position_t[num_anchors];
        /*!File.Exists(Application.persistentDataPath + "/anchor_settings.json")*/
        if (!File.Exists(Application.persistentDataPath + "/anchor_settings.json"))
        {
            anchor_positions[0] = new position_t(0, 0, 0);
            anchor_positions[1] = new position_t(203, -73, 0);
            anchor_positions[2] = new position_t(203, 73, 0);
            anchor_positions[3] = new position_t(-124, 73, 0);
            anchor_positions[4] = new position_t(-124, -73, 0);
            anchor_positions[5] = new position_t(0, 0, 0);
            //anchor_positions[0] = new position_t(0, 0, 0);
            //anchor_positions[1] = new position_t(350, -85, 0);
            //anchor_positions[2] = new position_t(350, 85, 0);
            //anchor_positions[3] = new position_t(-150, 85, 0);
            //anchor_positions[4] = new position_t(-150, -85, 0);
            //anchor_positions[5] = new position_t(0, 0, 0);

            for (int i = 0; i < num_anchors; i++)
            {
                m_AnchorPositionXAxis[i] = anchor_positions[i].x_pos_cm;
                m_AnchorPositionYAxis[i] = anchor_positions[i].y_pos_cm;
                m_AnchorPositionZAxis[i] = anchor_positions[i].z_pos_cm;
                (anchor_input[i].transform.GetChild(1).GetComponent<TMP_InputField>().text) = anchor_positions[i].x_pos_cm.ToString();
                (anchor_input[i].transform.GetChild(2).GetComponent<TMP_InputField>().text) = anchor_positions[i].y_pos_cm.ToString();
                (anchor_input[i].transform.GetChild(3).GetComponent<TMP_InputField>().text) = anchor_positions[i].z_pos_cm.ToString();
            }
            setAnchorPositions();
        }
        else
        {
            // if there is a config, read from it to get anchors/sliders
            readJson();

            //anchor_positions[0] = new position_t(0, 0, 0);
            //anchor_positions[1] = new position_t(203, -73, 0);
            //anchor_positions[2] = new position_t(203, 73, 0);
            //anchor_positions[3] = new position_t(-124, 73, 0);
            //anchor_positions[4] = new position_t(-124, -73, 0);
            //anchor_positions[5] = new position_t(0, 0, 0);

            ////anchor_positions[0] = new position_t(0, 0, 0);
            ////anchor_positions[1] = new position_t(71, -28, 0);
            ////anchor_positions[2] = new position_t(71, 28, 0);
            ////anchor_positions[3] = new position_t(-51, 28, 0);
            ////anchor_positions[4] = new position_t(-51, -28, 0);
            ////anchor_positions[5] = new position_t(0, 0, 0);
            //for (int i = 0; i < num_anchors; i++)
            //{
            //    m_AnchorPositionXAxis[i] = anchor_positions[i].x_pos_cm;
            //    m_AnchorPositionYAxis[i] = anchor_positions[i].y_pos_cm;
            //    m_AnchorPositionZAxis[i] = anchor_positions[i].z_pos_cm;
            //    (anchor_input[i].transform.GetChild(1).GetComponent<TMP_InputField>().text) = anchor_positions[i].x_pos_cm.ToString();
            //    (anchor_input[i].transform.GetChild(2).GetComponent<TMP_InputField>().text) = anchor_positions[i].y_pos_cm.ToString();
            //    (anchor_input[i].transform.GetChild(3).GetComponent<TMP_InputField>().text) = anchor_positions[i].z_pos_cm.ToString();
            //}
            //setAnchorPositions();
        }
        inner_rect = GameObject.Find("inner_rect");

        //key position text
        key_text1 = GameObject.Find("k2");
        key_text2 = GameObject.Find("k3");
        announcement_text = GameObject.Find("announcement_text");

        StartCoroutine(MovePersonAfterDelay());
    }

    // for saving to a json config
    [Serializable]
    public struct testStruct
    {
        public int[] AnchorPositionXAxis;
        public int[] AnchorPositionYAxis;
        public int[] AnchorPositionZAxis;
        public float t1;
        public float t2;
        public float t3;
        public float update;
        public float delay;
    }

    // save config; saves anchor positions and slider values
    public void saveJson()
    {
        testStruct data = new testStruct();
        data.AnchorPositionXAxis = m_AnchorPositionXAxis;
        data.AnchorPositionYAxis = m_AnchorPositionYAxis;
        data.AnchorPositionZAxis = m_AnchorPositionZAxis;
        data.t1 = slider1.GetComponent<UnityEngine.UI.Slider>().value;
        data.t2 = slider2.GetComponent<UnityEngine.UI.Slider>().value;
        data.t3 = slider3.GetComponent<UnityEngine.UI.Slider>().value;
        data.update = update_val.GetComponent<UnityEngine.UI.Slider>().value;
        data.delay = delay_val.GetComponent<UnityEngine.UI.Slider>().value;
        string new_json = JsonUtility.ToJson(data);
        System.IO.File.WriteAllText(Application.persistentDataPath + "/anchor_settings.json", new_json);
    }

    // read config
    public void readJson()
    {
        string dataAsJson = File.ReadAllText(Application.persistentDataPath + "/anchor_settings.json");
        testStruct data = JsonUtility.FromJson<testStruct>(dataAsJson);
        Debug.Log(JsonUtility.ToJson(data));

        m_AnchorPositionXAxis = data.AnchorPositionXAxis;
        m_AnchorPositionYAxis = data.AnchorPositionYAxis;
        m_AnchorPositionZAxis = data.AnchorPositionZAxis;
        for (int i = 0; i < num_anchors; i++)
        {
            anchor_positions[i].x_pos_cm = m_AnchorPositionXAxis[i];
            anchor_positions[i].y_pos_cm = m_AnchorPositionYAxis[i];
            anchor_positions[i].z_pos_cm = m_AnchorPositionZAxis[i];
            (anchor_input[i].transform.GetChild(1).GetComponent<TMP_InputField>().text) = anchor_positions[i].x_pos_cm.ToString();
            (anchor_input[i].transform.GetChild(2).GetComponent<TMP_InputField>().text) = anchor_positions[i].y_pos_cm.ToString();
            (anchor_input[i].transform.GetChild(3).GetComponent<TMP_InputField>().text) = anchor_positions[i].z_pos_cm.ToString();
        }
        setAnchorPositions();
        slider1.GetComponent<UnityEngine.UI.Slider>().value = data.t1;
        slider2.GetComponent<UnityEngine.UI.Slider>().value = data.t2;
        slider3.GetComponent<UnityEngine.UI.Slider>().value = data.t3;
        update_val.GetComponent<UnityEngine.UI.Slider>().value = data.update;
        delay_val.GetComponent<UnityEngine.UI.Slider>().value = data.delay;
    }

    // on close of app, save config
    void OnApplicationQuit()
    {
        Debug.Log("Application ending after " + Time.time + " seconds");
        saveJson();
    }

    // Update is called once per frame, updates available ports that can be opened
    void Update()
    {
        // update port dropdown
        List<String> ports = new List<String>();
        ports = SerialPort.GetPortNames().ToList();
        dropdown.ClearOptions();
        dropdown.AddOptions(ports);
       
        if (serial.IsOpen)
        {

        }
        else
        {
            
        }
    }

    public void openPort(string test)
    {
        string port = "\\\\.\\";
        string selectedPort = dropdown.options[dropdown.value].text;
        port += selectedPort;
        print("Opening port: " + port);

        // specify port instead of random
        serial.PortName = selectedPort;
        serial.Open();
        StartCoroutine(ReadSerial());
    }

    public void closePort(string test)
    {
        serial.Close();
    }

    // start reading if bytes are in buffer
    // check if first two bytes are 0x5A and 0xA5 (can skip 0x5A as well)
    // then read frame_type and data_len bytes; then read data_len amount of bytes
    // finally read/check the checksum byte at the end
    // if true, then decoding data was successfull, use localization algorithms to get position and move person
    IEnumerator ReadSerial()
    {
        byte[] Data = new byte[200];
        while (true)
        {
            if (serial.BytesToRead > 0)
            {
                try
                {

                    //Debug.Log("Starting serial read:");
                    int data_len = 0;
                    int frame_type = 0;

                    //check if read byte is the starting bytes of the frame, then read frame type and data length from data
                    int b1 = serial.ReadByte();
                    if (b1 == 0x5A)
                    {
                        int b2 = serial.ReadByte();
                        if (b2 == 0xA5)
                        {
                            frame_type = serial.ReadByte();
                            data_len = serial.ReadByte();
                            if (data_len > 250)
                            {
                                data_len = 0;
                                continue;
                            }
                        }
                    }
                    else if (b1 == 0xA5)
                    {
                        frame_type = serial.ReadByte();
                        data_len = serial.ReadByte();
                        if (data_len > 250)
                        {
                            data_len = 0;
                            continue;
                        }
                    }


                    if (data_len != 0 && data_len <= Data.Length)
                    {
                        //Debug.Log("\theader successful, performing checksum");
                        //check for checksum
                        int sum = 0;
                        for (int i = 0; i < data_len-1; i++)
                        {
                            //Data[i] = serial.ReadByte();
                            serial.Read(Data, i, 1);
                            sum += Data[i];
                            //print("\t +"+Data[i]);
                        }
                        int t = serial.ReadByte();
                        //print((sum & 0x0ff )+ " == " + t + "?");

                        if (((sum & 0x0ff) == t) && (data_len > 0)) 
                        {
                            RSDecodeStatus = DECODE_SUCESSFUL;
                            //Debug.Log("DECODESTATUS IS SUCCESSFUL");
                        }
                        else
                        {
                            RSDecodeStatus = 0;     
                            data_len = 0;
                        }
                    }
                    else
                    {
                        RSDecodeStatus = 0;
                    }

                    // If read data is correct, then start decoding it to get position, then move person
                    // If read data is just a confirmation of a successful written config, display msg
                    if (RSDecodeStatus == DECODE_SUCESSFUL)
                    {
                        //Debug.Log("\tDECODE SUCCESS, data len is: " + data_len);
                        const int UART_COMM_WRITE_CONFIG = 0xC1;
                        const int UART_COMM_LOCALIZATION_INFO = 0xC3;
                        switch (frame_type)
                        {
                            case UART_COMM_LOCALIZATION_INFO:
                                //Debug.Log("\tLOCALIZATION HEADER READ: Running getPosition...");
                                // get position, then update labels on UI, then begin proportional movement 
                                position_t result_pos = GetPosition(Data);
                                print("Final result position: " + result_pos.x_pos_cm+", "+ result_pos.y_pos_cm+", "+ result_pos.z_pos_cm);
                                key_text1.GetComponent<TMP_Text>().text = "X: " + result_pos.x_pos_cm + " cm";
                                key_text2.GetComponent<TMP_Text>().text = "Y: " + result_pos.y_pos_cm + " cm";
                                double newx = result_pos.x_pos_cm * proportion;
                                double newy = result_pos.y_pos_cm * proportion;
                                double newz = result_pos.z_pos_cm * proportion;
                                Debug.Log("Moving to: " + newx + " "+newy +" "+ newz);
                                move(person, (float)newx,person.transform.position.y,(float)newy);
                                break;
                            case UART_COMM_WRITE_CONFIG:
                                Debug.Log("\tWRITE CONFIG HEADER: checking validity...");
                                if (Data[0] == 0)
                                {
                                    print("\tWRITE CONFIG SUCCESS");
                                    announcement_text.GetComponent<TMP_Text>().text = "Write Config Successful";
                                    StartCoroutine(AnnouncementTimer());
                                }
                                else
                                    print("\tWRITE CONFIG ERROR");
                                break;
                            default:
                                print("\tUNRECOGNIZED FRAME TYPE RECIEVED");
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("Failed to read from serial port: " + ex.Message);
                }
            }
            yield return null; // Wait for the next frame
        }
    }
    Vector3 comparison;
    Vector3 init;
    int cnt = 0;
    bool outlier;

    public void toggleOutlierRemoval(bool toggle)
    {
        outlier = toggle;
    }

    // Move person to new position
    public void move(GameObject obj, float newX, float newY, float newZ)
    {
        Vector3 temp = new Vector3(newX, newY, newZ);

        //delay so that key registers correct position (will send 0,0,0 if not)
        if (cnt < 20) { cnt++; return; }

        //bool zero = false;
        //if (temp.x == 0.00 && temp.y == 0.00 && temp.z == 0.00) { zero = true; }
        //if (temp != Vector3.zero && !zero && init == null)
        //{
        //    init = temp;
        //    return;
        //}
        //if (init != Vector3.zero)
        //{
        //    print("NOT ZERO");
        //}
        //print(init);

        if (outlier) // if outlier removal is enabled,
        {
            //get comparison vector as average of buffer
            if (OutlierBuffer.Count() > 0)
            {
                comparison = OutlierBuffer.Aggregate(new Vector3(0, 0, 0), (s, v) => s + v) / (float)OutlierBuffer.Count;
                float distance = Vector3.Distance(comparison, temp);
                print("DISTANCE: " + distance.ToString());
                if (distance > float.Parse(outlier_limit.text))
                {
                    print("OUTLIER");
                    return;
                }
            }

            if (OutlierBuffer.Count > 3)
            {
                OutlierBuffer.RemoveAt(0); //remove earliest position
            }
            OutlierBuffer.Add(temp);
        }
        MoveVectors.Add(temp);
    }

    // Smoothen movement
    IEnumerator moveSmooth(GameObject obj, Vector3 target)
    {
        obj.transform.position = Vector3.Lerp(obj.transform.position, target, 1F);
        yield return null;
    }

    IEnumerator MovePersonAfterDelay()
    {
        while (true)
        {
            if (MoveVectors.Count() >= update_val.GetComponent<Slider>().value)
            {
                Vector3 new_pos = MoveVectors.Aggregate(new Vector3(0, 0, 0), (s, v) => s + v) / (float)MoveVectors.Count;

                //var bounds = new Bounds(Vector3.zero, Vector3.zero);
                //MoveVectors.ForEach(v => bounds.Encapsulate(v));
                //Vector3 new_pos = bounds.center;

                print("Starting Move with Count of " + MoveVectors.Count());
                StartCoroutine(moveSmooth(person, new_pos));
                MoveVectors.Clear();
            }
            yield return new WaitForSeconds(delay_val.GetComponent<Slider>().value);
        }
    }

    public void sendWriteConfig()
    {
        setAnchorPositions();
        //StartCoroutine(SendSerial());
    }

    //create data bytes containg configuration, then send along with header bytes to the device
    IEnumerator SendSerial()
    {
        Debug.Log("Serial send started...");
        int polling_time = 100;
        int WelcomeThreshold = 500;
        int UnlockThreshold = 300;
        int InsideThreshold = 50;
        byte[] txBuf = new byte[100];
        /* polling time */
        txBuf[0] = (byte)(((polling_time & 0xFFFF) >> 8) & 0xFF);
        txBuf[1] = (byte)(polling_time & 0xFF);
        /* A2 Axis*/
        txBuf[2] = (byte)(Math.Abs(m_AnchorPositionXAxis[UWB_ANCHOR_RL]) & 0xFF);
        txBuf[3] = (byte)(((Math.Abs(m_AnchorPositionXAxis[UWB_ANCHOR_RL]) & 0xFFFF) >> 8) & 0xFF);
        txBuf[4] = (byte)(Math.Abs(m_AnchorPositionXAxis[UWB_ANCHOR_RL]) & 0xFF);
        txBuf[5] = (byte)(Math.Abs(m_AnchorPositionZAxis[UWB_ANCHOR_RL]) & 0xFF);
        /* A3 Axis*/
        txBuf[6] = (byte)(Math.Abs(m_AnchorPositionYAxis[UWB_ANCHOR_RR]) & 0xFF);
        txBuf[7] = (byte)(((Math.Abs(m_AnchorPositionXAxis[UWB_ANCHOR_RR]) & 0xFFFF) >> 8) & 0xFF);
        txBuf[8] = (byte)(Math.Abs(m_AnchorPositionXAxis[UWB_ANCHOR_RR]) & 0xFF);
        txBuf[9] = (byte)(Math.Abs(m_AnchorPositionZAxis[UWB_ANCHOR_RR]) & 0xFF);
        /* A4 Axis*/
        txBuf[10] = (byte)(Math.Abs(m_AnchorPositionYAxis[UWB_ANCHOR_FR]) & 0xFF);
        txBuf[11] = (byte)(((Math.Abs(m_AnchorPositionXAxis[UWB_ANCHOR_FR]) & 0xFFFF) >> 8) & 0xFF);
        txBuf[12] = (byte)(Math.Abs(m_AnchorPositionXAxis[UWB_ANCHOR_FR]) & 0xFF);
        txBuf[13] = (byte)(Math.Abs(m_AnchorPositionZAxis[UWB_ANCHOR_FR]) & 0xFF);
        /* A5 Axis*/
        txBuf[14] = (byte)(Math.Abs(m_AnchorPositionYAxis[UWB_ANCHOR_FL]) & 0xFF);
        txBuf[15] = (byte)(((Math.Abs(m_AnchorPositionXAxis[UWB_ANCHOR_FL]) & 0xFFFF) >> 8) & 0xFF);
        txBuf[16] = (byte)(Math.Abs(m_AnchorPositionXAxis[UWB_ANCHOR_FL]) & 0xFF);
        txBuf[17] = (byte)(Math.Abs(m_AnchorPositionZAxis[UWB_ANCHOR_FL]) & 0xFF);
        /* Unlock */
        txBuf[18] = (byte)((UnlockThreshold / 10) & 0xFF);
        /* welcome */
        txBuf[19] = (byte)((WelcomeThreshold / 10) & 0xFF);
        /* inside */
        txBuf[20] = (byte)((InsideThreshold / 10) & 0xFF);
        byte UART_COMM_WRITE_CONFIG = 0xC1;
        //print("Config send");
        if (true == UartSerialSending(UART_COMM_WRITE_CONFIG, txBuf, 21))
        {
            //m_bWriteConfigOperation = TRUE;
            //SetTimer(WRITE_OPERATION_TIMEOUT, 2000, NULL);
            print("finished sending, starting read coroutine...");
            StartCoroutine(TimeoutCoroutine());
            //StartCoroutine(ReadSerial());
        }
        yield break;
    }

    // timer for announcement text
    IEnumerator TimeoutCoroutine()
    {
        yield return new WaitForSeconds(2f);
        print("Write config timeout, try again");
        announcement_text.GetComponent<TMP_Text>().text = "Write Config Timed Out, Try Again";
        StartCoroutine(AnnouncementTimer());
    }

    // timer for clearing announcement text
    IEnumerator AnnouncementTimer()
    {
        yield return new WaitForSeconds(10f);
        announcement_text.GetComponent<TMP_Text>().text = "";
    }


    // Function for sending the config to the UWB system
    bool UartSerialSending(byte frametype, byte[] txbuf, int len)
    {
        byte checksum = 0;

        checksum = 0;

        byte[] ByteArraytemp = new byte[len+5];       

        byte RESPOND_START_BYTE1 = 0x5A; // header bytes
        byte RESPOND_START_BYTE2 = 0xA5;
        ByteArraytemp[0] = RESPOND_START_BYTE1;
        ByteArraytemp[1] = RESPOND_START_BYTE2;
        ByteArraytemp[2] = frametype;
        ByteArraytemp[3] = (byte)(len + 1);

        for (int i = 4; i < len+4; i++)             
        {
            checksum += txbuf[i];
            ByteArraytemp[i] = txbuf[i];
        }
        ByteArraytemp[len+4] = checksum;

        try
        {
            serial.Write(ByteArraytemp, 0, ByteArraytemp.Length);
            Debug.Log("wrote to serial");
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to write to serial port: " + ex.Message);

            return false;
        }

        return true;
        //return false;
    }

    // display anchor distance on GUI
    void displayAnchorPoint(GameObject anchor_text, string dist)
    {
        //string tag, string x, string y
        //anchor_text.transform.GetChild(0).GetComponent<TMP_Text>().text = tag;
        //anchor_text.transform.GetChild(1).GetComponent<TMP_Text>().text = x;
        //anchor_text.transform.GetChild(2).GetComponent<TMP_Text>().text = y;
        anchor_text.transform.GetChild(3).GetComponent<TMP_Text>().text = dist;
    }

    // Sets anchors to correct positions and updates text
    public void setAnchorPositions()
    {
        for (int i=0; i<num_anchors;i++)
        {
            anchor_positions[i].x_pos_cm = int.Parse(anchor_input[i].transform.GetChild(1).GetComponent<TMP_InputField>().text);
            anchor_positions[i].y_pos_cm = int.Parse(anchor_input[i].transform.GetChild(2).GetComponent<TMP_InputField>().text);
            anchor_positions[i].z_pos_cm = int.Parse(anchor_input[i].transform.GetChild(3).GetComponent<TMP_InputField>().text);
            if (i != num_anchors - 1) // exclude non existant anchor 6
            {
                anchor_text[i].transform.GetChild(1).GetComponent<TMP_Text>().text = anchor_positions[i].x_pos_cm.ToString();
                anchor_text[i].transform.GetChild(2).GetComponent<TMP_Text>().text = anchor_positions[i].y_pos_cm.ToString();
            }
            m_AnchorPositionXAxis[i] = int.Parse(anchor_input[i].transform.GetChild(1).GetComponent<TMP_InputField>().text);
            m_AnchorPositionYAxis[i] = int.Parse(anchor_input[i].transform.GetChild(2).GetComponent<TMP_InputField>().text);
            m_AnchorPositionZAxis[i] = int.Parse(anchor_input[i].transform.GetChild(3).GetComponent<TMP_InputField>().text);
        }
        updateAnchorPositions(anchor_positions);
    }


    // Performs updates on all objects so that size/position is correlated to new anchor positions, result is everything looks proportional
    public void updateAnchorPositions(position_t[] anchor_positions_cm_pt)
    {
        for (int i=0; i<num_anchors-1; i++)
        {
            float newX = anchor_positions_cm_pt[i].x_pos_cm * (float)proportion;
            float newY = anchor[i].transform.position.y; // y is always the same as the default for the gameobject; height never changes
            float newZ = anchor_positions_cm_pt[i].y_pos_cm * (float)proportion; // substitute actual y for gameobject z
            anchor[i].transform.position = new Vector3(newX, newY, newZ);
        }
        
        float new_size_len = (Math.Abs(anchor_positions[4].x_pos_cm) + Math.Abs(anchor_positions[1].x_pos_cm)) * (float)proportion;
        float new_size_wid = (Math.Abs(anchor_positions[4].y_pos_cm) + Math.Abs(anchor_positions[1].y_pos_cm)) * (float)proportion;

        new_size_len = (float)Math.Round(new_size_len,1);
        new_size_wid = (float)Math.Round(new_size_wid, 1);
        new_scale_len = new_size_len * (float)(0.5/ 1.5);
        new_scale_wid = new_scale_len;
        print(new_scale_len);

        ////car size, make bigger/smaller to fit anchors
        Vector3 scale_vector = new Vector3(new_scale_wid, new_scale_len, new_scale_len);
        car.transform.localScale = scale_vector;
        person.transform.localScale = scale_vector;

        float cx = (anchor[4].transform.position.x + anchor[1].transform.position.x) / 2;
        car.transform.position = new Vector3(cx, car.transform.position.y, car.transform.position.z);

        //-0.25 for anchor 0 to touch car 
        for (int i = 0; i < num_anchors - 1; i++)
        {
            float tmp = ascale/1.5F;
            anchor[i].transform.localScale = new Vector3(tmp * new_size_len, tmp * new_size_len, tmp * new_size_len);
            if (i == 0)
            {
                anchor[i].transform.position = new Vector3(anchor[i].transform.position.x, a0y * new_scale_len * 1.8F, anchor[i].transform.position.z);
            }
            else if (i==1||i==2)
            {
                anchor[i].transform.position = new Vector3(anchor[i].transform.position.x, a1y * new_scale_len * 1.8F, anchor[i].transform.position.z);
            }
            else if (i == 3 || i == 4)
            {
                anchor[i].transform.position = new Vector3(anchor[i].transform.position.x, a4y * new_scale_len * 1.8F, anchor[i].transform.position.z);
            }
        }

        float temp_scale = new_size_len/1.5F;

        // change ground size
        GameObject ground = GameObject.Find("GroundCube");
        ground.transform.localScale = new Vector3(temp_scale * groundscale.x, 1, temp_scale * groundscale.z);

        // update camera position/properties
        //cam1.transform.position = new Vector3(cam1.transform.position.x, cam1.transform.position.y * temp_scale, cam1.transform.position.z * temp_scale);
        //cam2.transform.position = new Vector3(cam2.transform.position.x * temp_scale, cam2.transform.position.y * temp_scale, cam2.transform.position.z * temp_scale);
        //cam3.transform.position = new Vector3(cam3.transform.position.x, cam3.transform.position.y * temp_scale, cam3.transform.position.z);
        cam1.transform.position = new Vector3(cam1.transform.position.x, c1y * temp_scale, c1z * temp_scale);
        cam2.transform.position = new Vector3(c2x * temp_scale, c2y * temp_scale, c2z * temp_scale);
        cam3.transform.position = new Vector3(cam3.transform.position.x, c3y * temp_scale, cam3.transform.position.z);

        // reset cameras
        cam1.SetActive(true);
        cam2.SetActive(false);
        cam3.SetActive(false);

        // update lighting for new size
        GameObject light = GameObject.Find("spotlight");
        light.transform.position = new Vector3(light.transform.position.x, lighty * temp_scale, light.transform.position.z);
        light.GetComponent<Light>().range = lightrange * temp_scale;

        // update sliders
        GameObject inside_t_slider = GameObject.Find("InsideThresholdSlider");
        GameObject unlock_t_slider = GameObject.Find("UnlockThresholdSlider");
        GameObject welcome_t_slider = GameObject.Find("WelcomeThresholdSlider");
        Transform slider1 = inside_t_slider.transform.GetChild(1);
        Transform slider2 = unlock_t_slider.transform.GetChild(1);
        Transform slider3 = welcome_t_slider.transform.GetChild(1);
        slider1.GetComponent<UnityEngine.UI.Slider>().value = 1 * temp_scale;
        slider2.GetComponent<UnityEngine.UI.Slider>().value = 2 * temp_scale;
        slider3.GetComponent<UnityEngine.UI.Slider>().value = 3 * temp_scale;

        //center
        GameObject threshold_1 = GameObject.Find("InsideThreshold");
        GameObject threshold_2 = GameObject.Find("UnlockThreshold");
        GameObject threshold_3 = GameObject.Find("WelcomeThreshold");
        threshold_1.transform.position = new Vector3(cx, threshold_1.transform.position.y, threshold_1.transform.position.z);
        threshold_2.transform.position = new Vector3(cx, threshold_2.transform.position.y, threshold_2.transform.position.z);
        threshold_3.transform.position = new Vector3(cx, threshold_3.transform.position.y, threshold_3.transform.position.z);
        light.transform.position = new Vector3(cx, light.transform.position.y, light.transform.position.z);
        cam1.transform.position = new Vector3(cx, cam1.transform.position.y, cam1.transform.position.z);
        cam2.transform.position = new Vector3(cam2.transform.position.x +cx, cam2.transform.position.y, cam2.transform.position.z + cx);
        cam3.transform.position = new Vector3(cx, cam3.transform.position.y, cam3.transform.position.z);
        ground.transform.position = new Vector3(cx, ground.transform.position.y, ground.transform.position.z);

    }

    float c1y; float c1z;
    float c2x; float c2y; float c2z;
    float c3y;
    Vector3 groundscale;
    float a0y; float a1y; float a4y; float ascale;
    float lighty; float lightrange;
    public void getInitialScales()
    {
        c1y = cam1.transform.position.y; c1z = cam1.transform.position.z;
        c2x = cam2.transform.position.x; c2y = cam2.transform.position.y; c2z = cam2.transform.position.z;
        c3y = cam3.transform.position.y;
        GameObject ground = GameObject.Find("GroundCube");
        groundscale = ground.transform.localScale;
        a0y = anchor[0].transform.position.y;
        a1y = anchor[1].transform.position.y;
        a4y = anchor[4].transform.position.y;
        ascale = anchor[0].transform.localScale.x;
        GameObject light = GameObject.Find("spotlight");
        lighty = light.transform.position.y;
        lightrange =  light.GetComponent<Light>().range;
    }

    // Below are Localization ALgorithms (GetPosition, UpdataLocalizationInfo, Localization, calculatePosition3D) which match the same alogrithms used by 2D application
    // Used to calculate position of key based on:
    // - array of distances from anchor
    // - anchor positions
    public position_t GetPosition(byte[] read_data)
    {
        uwbinfo_t m_UWBInfo = new uwbinfo_t(num_anchors);
        for (int i = 0; i < num_anchors; i++)
        {
            m_UWBInfo.anchorPositionsX[i] = m_AnchorPositionXAxis[i] * 10;
            //print(i);
            //print(m_AnchorPositionXAxis[i]);
            m_UWBInfo.anchorPositionsY[i] = m_AnchorPositionYAxis[i] * 10;
            m_UWBInfo.anchorPositionsZ[i] = m_AnchorPositionZAxis[i] * 10;
            m_UWBInfo.anchorsMeasuredDistance[i] = INVALID;
            m_UWBInfo.rangingStatus[i] = INVALID;
        }

        //byte[] Data = OnRxComm(); // here is where we put the "read data" function
        //UpdataLocalizationInfo(Data, 23, true, ref m_UWBInfo); // updates m_uwbINFO
        UpdataLocalizationInfo(read_data, 23, true, ref m_UWBInfo); // updates m_uwbINFO
        position_t temp = Localization(m_UWBInfo); // will call calc pos
        //print("--------------------------");
        //print("CALCULATED POSITION: ");
        //print(temp.x_pos_cm);
        //print(temp.y_pos_cm);
        return temp;
    }

    static byte RSDecodeStatus = 0;
    const byte DECODE_SUCESSFUL = 0x0a;

    bool UpdataLocalizationInfo(byte[] buf, int len, bool hasLock8Info, ref uwbinfo_t m_UWBInfo)
    {
        const int UWB_ANCHOR_INSIDE = 0;
        const int UWB_ANCHOR_RL = 1;
        const int UWB_ANCHOR_RR = 2;
        const int UWB_ANCHOR_FR = 3;
        const int UWB_ANCHOR_FL = 4;
        const int UWB_ANCHOR_INSIDE2 = 5;
        //const byte AnchorIndex[UWB_ANCHOR_QUANTITY] = { UWB_ANCHOR_INSIDE, UWB_ANCHOR_RL, UWB_ANCHOR_RR, UWB_ANCHOR_FR, UWB_ANCHOR_FL, UWB_ANCHOR_INSIDE2 };
        int[] AnchorIndex = { UWB_ANCHOR_INSIDE, UWB_ANCHOR_RL, UWB_ANCHOR_RR, UWB_ANCHOR_FR, UWB_ANCHOR_FL, UWB_ANCHOR_INSIDE2 };
        int i;
        int index;
        UInt32 distance;
        UInt32 position;
        bool positionIsInvalid = false;

            position_t m_KobPos;

        if (hasLock8Info == true)
        {
            if (len != 23)
            {
                return false;
            }
        }
        else
        {
            if (len != 22)
            {
                return false;
            }
        }

        for (i = 0; i < 15; i += 3)
        {
            distance = ((UInt32)buf[i + 0] << 16) | ((UInt32)buf[i + 1] << 8) | ((UInt32)buf[i + 2]);
            index = AnchorIndex[i / 3];
            if (distance == 0xFFFFFF)
            {
                m_UWBInfo.anchorsMeasuredDistance[index] = INVALID;
                m_UWBInfo.rangingStatus[index] = SUCCESS;
            }
            else
            {
                m_UWBInfo.anchorsMeasuredDistance[index] = (double)distance / 1000;
                m_UWBInfo.rangingStatus[index] = SUCCESS;
            }
        }
        if ((hasLock8Info == false) && (len == 22))
        {
            distance = ((UInt32)buf[19] << 16) | ((UInt32)buf[20] << 8) | ((UInt32)buf[21]);
            index = AnchorIndex[UWB_ANCHOR_INSIDE2];
            if (distance == 0xFFFFFF)
            {
                m_UWBInfo.anchorsMeasuredDistance[index] = INVALID;
                m_UWBInfo.rangingStatus[index] = SUCCESS;
            }
            else
            {
                m_UWBInfo.anchorsMeasuredDistance[index] = (double)distance / 1000;
                m_UWBInfo.rangingStatus[index] = SUCCESS;
            }
        }
        else
        {
            m_UWBInfo.anchorsMeasuredDistance[AnchorIndex[UWB_ANCHOR_INSIDE2]] = INVALID;
            m_UWBInfo.rangingStatus[AnchorIndex[UWB_ANCHOR_INSIDE2]] = SUCCESS;
        }


        position = ((UInt32)buf[15] << 8) | ((UInt32)buf[16]);
        if (position != 0xFFFF)
        {
            m_KobPos.x_pos_cm = (Int32)position - 30000;
        }
        else
        {
            positionIsInvalid = true;
        }
        position = ((UInt32)buf[17] << 8) | ((UInt32)buf[18]);
        if (position != 0xFFFF)
        {
            m_KobPos.y_pos_cm = (Int32)position - 30000;
        }
        else
        {
            positionIsInvalid = true;
        }
        m_KobPos.z_pos_cm = 0;

        return true;
    }

    // SET ANCHOR POSITIONS BEFORE
    position_t Localization(uwbinfo_t sUwbInfo)
    {
        // Localization variables
        position_t[] anchor_pos_cm_pt = new position_t[num_anchors];
        UInt32[] ranging_results_cm_pt = new UInt32[num_anchors];

        position_t result_position;

        int index = 0;
        int index_success = 0;

        /* Search for valid UWB Distance measurements for the localization */
        for (index = 0; index < num_anchors; index++)
        {
            if (sUwbInfo.rangingStatus[index] == SUCCESS)
            {
                anchor_pos_cm_pt[index_success].x_pos_cm = sUwbInfo.anchorPositionsX[index] / ((Int32)10); // convert mm into cm for NXPLocalization_CalculatePosition3D function
                anchor_pos_cm_pt[index_success].y_pos_cm = sUwbInfo.anchorPositionsY[index] / ((Int32)10); // convert mm into cm for NXPLocalization_CalculatePosition3D function
                anchor_pos_cm_pt[index_success].z_pos_cm = sUwbInfo.anchorPositionsZ[index] / ((Int32)10); // convert mm into cm for NXPLocalization_CalculatePosition3D function
                ranging_results_cm_pt[index_success] = (UInt32)(sUwbInfo.anchorsMeasuredDistance[index] * ((double)100)); // convert m into cm for the localization algorithm
                // display
                if (index != num_anchors - 1)
                {
                    displayAnchorPoint(anchor_text[index_success], ranging_results_cm_pt[index_success].ToString());
                }
                index_success++;
            }
            else
            {
                // Do nothing
            }
        }

        result_position = calculatePosition3D(ranging_results_cm_pt, index_success, anchor_pos_cm_pt, index_success);
        return result_position;
    }

    // Use z=0; since this application is only moving the person in 2D
    public position_t calculatePosition3D(UInt32[] distances_cm_pt, int no_distances, position_t[] anchor_positions_cm_pt, int no_anc_positions)
    {
        // store results
        position_t position_result_pt = new position_t();

        /* Matrix components (3*3 Matrix resulting from least square error method) [cm^2] */
        Int64 M_11 = 0;
        Int64 M_12 = 0;                                                                                       // = M_21
        Int64 M_13 = 0;                                                                                       // = M_31
        Int64 M_22 = 0;
        Int64 M_23 = 0;                                                                                       // = M_23
        Int64 M_33 = 0;
        /* Vector components (3*1 Vector resulting from least square error method) [cm^3] */
        Int64 b_1 = 0;
        Int64 b_2 = 0;
        Int64 b_3 = 0;
        /* Miscellaneous variables */
        //position_status_t status = { success, fct_nxplocalization_calculatepostion2d, file_nxplocalization };
        uint index = 0U;
        Int64 temp = 0;
        Int64 nominator = 0;
        Int64 denominator = 0;


        /* Check, if enough anchors are used to do localization */
        if (no_distances < 3) /* calculate z with pythagoras*/
        {
            print("ERROR: # Distances < 3");

            goto END_OF_FCT;
        }


        /* Check, if there are as many distances as anchor positions */
        if (no_distances != no_anc_positions)
        {
            print("ERROR: # anchors != # distances");
            goto END_OF_FCT;
        }

        {
            /* Writing values resulting from least square error method (A_trans*A*x = A_trans*r; row 0 was used to remove x^2,y^2,z^2 entries => index starts at 1) */
            for (index = 1u; index < no_distances; index++)
            {
                /*Matrix (needed to be multiplied with 2, afterwards):  M = A' * A */
                M_11 += (Int64)Math.Pow((anchor_positions_cm_pt[index].x_pos_cm - anchor_positions_cm_pt[0u].x_pos_cm), 2);
                M_12 += (Int64)(((Int64)anchor_positions_cm_pt[index].x_pos_cm - (Int64)anchor_positions_cm_pt[0u].x_pos_cm) * ((Int64)anchor_positions_cm_pt[index].y_pos_cm - (Int64)anchor_positions_cm_pt[0u].y_pos_cm));
                M_13 += (Int64)(((Int64)anchor_positions_cm_pt[index].x_pos_cm - (Int64)anchor_positions_cm_pt[0u].x_pos_cm) * ((Int64)anchor_positions_cm_pt[index].z_pos_cm - (Int64)anchor_positions_cm_pt[0u].z_pos_cm));
                M_22 += (Int64)Math.Pow((anchor_positions_cm_pt[index].y_pos_cm - anchor_positions_cm_pt[0u].y_pos_cm), 2);
                M_23 += (Int64)(((Int64)anchor_positions_cm_pt[index].y_pos_cm - (Int64)anchor_positions_cm_pt[0u].y_pos_cm) * ((Int64)anchor_positions_cm_pt[index].z_pos_cm - (Int64)anchor_positions_cm_pt[0u].z_pos_cm));
                M_33 += (Int64)Math.Pow((anchor_positions_cm_pt[index].z_pos_cm - anchor_positions_cm_pt[0u].z_pos_cm), 2);

                /* Vector: b_ = b * temp */
                temp = (Int64)((Int64)Math.Pow(distances_cm_pt[0], 2) - (Int64)Math.Pow(distances_cm_pt[index], 2)
                    + (Int64)Math.Pow(anchor_positions_cm_pt[index].x_pos_cm, 2) + (Int64)Math.Pow(anchor_positions_cm_pt[index].y_pos_cm, 2) + (Int64)Math.Pow(anchor_positions_cm_pt[index].z_pos_cm, 2)
                    - (Int64)Math.Pow(anchor_positions_cm_pt[0u].x_pos_cm, 2) - (Int64)Math.Pow(anchor_positions_cm_pt[0u].y_pos_cm, 2) - (Int64)Math.Pow(anchor_positions_cm_pt[0u].z_pos_cm, 2));
                b_1 += (Int64)(((Int64)anchor_positions_cm_pt[index].x_pos_cm - (Int64)anchor_positions_cm_pt[0u].x_pos_cm) * temp);
                b_2 += (Int64)(((Int64)anchor_positions_cm_pt[index].y_pos_cm - (Int64)anchor_positions_cm_pt[0u].y_pos_cm) * temp);
                b_3 += (Int64)(((Int64)anchor_positions_cm_pt[index].z_pos_cm - (Int64)anchor_positions_cm_pt[0u].z_pos_cm) * temp);

            }

            M_11 = 2 * M_11;
            M_12 = 2 * M_12;
            M_13 = 2 * M_13;
            M_22 = 2 * M_22;
            M_23 = 2 * M_23;
            M_33 = 2 * M_33;

            /* Calculating the z-position, if calculation is possible (at least one anchor at z != 0) */
            if ((M_13 + M_23 + M_33) != 0u)
            {
                nominator = b_1 * (M_12 * M_23 - M_13 * M_22) + b_2 * (M_12 * M_13 - M_11 * M_23) + b_3 * (M_11 * M_22 - M_12 * M_12);          // [cm^7]
                denominator = M_11 * (M_33 * M_22 - M_23 * M_23) + 2 * M_12 * M_13 * M_23 - M_33 * M_12 * M_12 - M_22 * M_13 * M_13;                // [cm^6]

                position_result_pt.z_pos_cm = (Int32)(((nominator * 10) / denominator + 5) / 10);                                    // [cm]
            }
            /* Else prepare for different calculation approach (after x and y were calculated) */
            else
            {
                //position_result_pt.z_pos_cm = 0u;
                position_result_pt.z_pos_cm = 0; 
            }

            /* Calculating the y-position */
            nominator = b_2 * M_11 - b_1 * M_12 - ((Int64)position_result_pt.z_pos_cm) * (M_11 * M_23 - M_12 * M_13);                    // [cm^5]
            denominator = M_11 * M_22 - M_12 * M_12;                                                                                // [cm^4]

            position_result_pt.y_pos_cm = (Int32)(((nominator * 10) / denominator + 5) / 10);                                        // [cm]

            /* Calculating the x-position */
            nominator = b_1 - ((Int64)position_result_pt.z_pos_cm) * M_13 - ((Int64)position_result_pt.y_pos_cm) * M_12;      // [cm^3]
            denominator = M_11;                                                                                                 // [cm^2]

            position_result_pt.x_pos_cm = (Int32)(((nominator * 10) / denominator + 5) / 10);                                        // [cm]

            /* Calculate z-position form x and y coordinates, if z can't be determined by previous steps (All anchors at z_n = 0) */
            if ((M_13 + M_23 + M_33) == 0u)
            {
                for (index = 0; index < no_distances; index++)
                {
                    //
                    temp = (Int64)((Int64)Math.Pow(distances_cm_pt[index], 2)
                        - (Int64)Math.Pow((double)((Int64)position_result_pt.x_pos_cm - (Int64)anchor_positions_cm_pt[index].x_pos_cm), 2)
                        - (Int64)Math.Pow((double)((Int64)position_result_pt.y_pos_cm - (Int64)anchor_positions_cm_pt[index].y_pos_cm), 2));

                    //
                    if (temp >= 0)
                    {
                        position_result_pt.z_pos_cm += (Int32)Math.Sqrt((double)temp);
                    }
                    else
                    {
                        position_result_pt.z_pos_cm = 0;
                    }
                }
                position_result_pt.z_pos_cm = position_result_pt.z_pos_cm / no_distances; // Divide sum by number of distances to get the average
            }
        }


        END_OF_FCT:;     // empty statement
        return position_result_pt;
    }

}