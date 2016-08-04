using UnityEngine;
using System.Collections;

public class MildPlayer : MonoBehaviour {

	public float moveSpeed = 5f;
	public float turnSpeed = 90f;

	Vector3 targetSyncPosition = Vector3.zero;
	Quaternion targetSyncRotation = Quaternion.identity;
	Vector3 targetSyncScale = Vector3.one;
	bool isMyPlayer { get { return Mild.IsMyTransform( transform ); } }

	// Use this for initialization
	void Start () {

	}

	// Update_ functions run automatically every frame, called by Mild.cs

	// UpdateMyPlayer is called only on my player
	void UpdateMyPlayer () {
		// basic input
		float vertical = Input.GetAxis("Vertical");
		float horizontal = Input.GetAxis("Horizontal");

		transform.Translate(0f, 0f, vertical * moveSpeed * Time.deltaTime);
		transform.Rotate( 0f, horizontal * turnSpeed * Time.deltaTime, 0f);
	}

	// UpdateOtherPlayer is called only on local copies of other players
	void UpdateOtherPlayer () {
		// move other player's object toward where they said they are
		if ( Vector3.Distance( transform.position, targetSyncPosition ) > moveSpeed ) {
			transform.position = targetSyncPosition; // teleport if it's very far away
		} else { // ... otherwise, just move toward it (this will happen most of the time)
			transform.position = Vector3.MoveTowards( transform.position, targetSyncPosition, moveSpeed * Time.deltaTime );
		}
		transform.rotation = Quaternion.Slerp( transform.rotation, targetSyncRotation, Time.deltaTime * 10f);
		transform.localScale = targetSyncScale;
	}

	// Tick_ functions run automatically based on Mild.tickRate

	// TickMyPlayer is automatically called only on MY PLAYER's player object... use this to send data
	void TickMyPlayer () {
		// these commands get broadcast on everyone's copy of a player object
		if ( Vector3.Distance( targetSyncPosition, transform.position ) >= 0.01f ) {
			targetSyncPosition = transform.position;
			Mild.CmdVec3( "SyncPosition", transform.position ); 
		}
		if ( Quaternion.Angle( targetSyncRotation, transform.rotation ) >= 5f ) {
			targetSyncRotation = transform.rotation;
			Mild.CmdQuat( "SyncRotation", transform.rotation );
		}
			
		if ( Vector3.Distance( targetSyncScale, transform.localScale ) >= 0.01f ) {
			targetSyncScale = transform.localScale;
			Mild.CmdVec3( "SyncScale", transform.localScale ); 
		}
	}

	// TickOtherPlayer is automatically called only on OTHER PLAYER's player objects, use this to process the data or do other stuff
	void TickOtherPlayer () {
		
	}

	void SyncPosition (Vector3 newPosition) {
		targetSyncPosition = newPosition;
	}

	void SyncRotation (Quaternion newRotation) {
		targetSyncRotation = newRotation;
	}

	void SyncScale (Vector3 newScale) {
		targetSyncScale = newScale;
	}


	void OnPlayerJoin () {
		Mild.CmdVec3( "SyncPosition", transform.position );
		Mild.CmdQuat( "SyncRotation", transform.rotation );
		Mild.CmdVec3( "SyncScale", transform.localScale ); 
	}

}
