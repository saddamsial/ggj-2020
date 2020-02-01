﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomInhabitantComponent : MonoBehaviour
{
  public RoomComponent Room => _roomComponent;
  public InteratibleDeviceComponent CurrentDevice => _currentDevice;

  private RoomComponent _roomComponent;
  private InteratibleDeviceComponent _currentDevice;

  private bool _isBeingSuckedIntoSpace = false;
  public bool IsBeingSuckedIntoSpace
  {
    get { return _isBeingSuckedIntoSpace; }
  }

  [SerializeField]
  private Rigidbody _rigidBody = null;

  public Rigidbody PhysicsRigidBody
  {
    get { return _rigidBody; }
  }

  public virtual void OnRoomEntered(RoomComponent room)
  {
    _roomComponent = room;
  }

  public virtual void OnRoomExited(RoomComponent room)
  {
    if (room == _roomComponent)
    {
      _roomComponent = null;
    }
  }

  public virtual void OnInteractionEntered(InteratibleDeviceComponent Device)
  {
    _currentDevice = Device;
  }

  public virtual void OnInteractionExited(InteratibleDeviceComponent Device)
  {
    if (_currentDevice == Device)
    {
      _currentDevice = null;
    }
  }

  public void NotifySuckedIntoSpace()
  {
    _isBeingSuckedIntoSpace = true;
  }
}
