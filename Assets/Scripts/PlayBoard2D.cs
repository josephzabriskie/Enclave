﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CellTypes;
using PlayerActions;
using UnityEngine.Tilemaps;

//!! Important This script should be high priority in execution order
public class PlayBoard2D : MonoBehaviour {
	public GameObject gridPrefab;
	float midSpace = 1.0f;
	public GameGrid2D playerGrid = null;
	public GameGrid2D enemyGrid = null;
	public int sizex;
	public int sizey;
	public PlayerConnectionObj pobj;
	public Vector3 playerShotOrig;
	public Vector3 enemyShotOrig;
	List<ActionReq> lastActions = new List<ActionReq>{};
	public pAction actionContext = pAction.noAction;
	public GameObject defaultProjectile;
	public GameObject defaultBuild;
	public GameObject fireMultiObj;
	bool fancyUpdateRunning = false;

	void Awake(){
		this.InstantiateGrids();
		playerShotOrig = this.transform.Find("PlayerOrigin").transform.position;
		enemyShotOrig = this.transform.Find("EnemyOrigin").transform.position;
	}

	//Just set the gridstates with no ceremony
	public void SetGridStates(CellStruct[,] pGrid, CellStruct[,] eGrid){
		this.playerGrid.SetCSArray(pGrid);
		this.enemyGrid.SetCSArray(eGrid);
	}

	public void UpdateBoardFancy(List<ActionReq> actions, CellStruct[,] pGrid, CellStruct[,] eGrid){
		StartCoroutine(IEUpdateBoardFancy(actions, pGrid, eGrid));
		fancyUpdateRunning = true;
	}


	public IEnumerator IEUpdateBoardFancy(List<ActionReq> actions, CellStruct[,] pGrid, CellStruct[,] eGrid){
		Debug.Log("Got input of " + actions.Count.ToString() + " actions");
		int rem;
		List<ActionReq> nextActions = getNextActions(actions, out rem);
		int maxloops = 20; // TODO remove when done
		int loops = 0; // TODO remove when done
		while(nextActions != null && loops < maxloops){
			loops++;
			Debug.LogFormat("input ac count: {0}, next ac count: {1}", actions.Count, nextActions.Count);
			switch(nextActions[0].a){
			case pAction.buildIntelTower:
			case pAction.buildOffenceTower:
			case pAction.buildDefenceTower:
			case pAction.buildWall:
			case pAction.buildReflector:
			case pAction.buildDefenceGrid:
			case pAction.placeMine:
			{
				Debug.Log("In the build section of fancy update");
				ActionResolution buildRes = Instantiate(defaultBuild).GetComponent<ActionResolution>();
				buildRes.Init(nextActions, this, pGrid, eGrid);
				yield return buildRes.IEResolve();
				break;
			}
			case pAction.fireBasic:
			case pAction.fireAgain:
			case pAction.firePiercing:
			{
				Debug.Log("In the fire section of fancy update");
				ActionResolution fireRes = Instantiate(defaultProjectile).GetComponent<ActionResolution>();
				fireRes.Init(nextActions, this, pGrid, eGrid);
				yield return fireRes.IEResolve();
				break;
			}
			case pAction.fireSquare:
			case pAction.hellFire:
			case pAction.fireRow:
			{
				Debug.Log("In the fire multi section of fancy update");
				ActionResolution fireMultiRes = Instantiate(fireMultiObj).GetComponent<ActionResolution>();
				fireMultiRes.Init(nextActions, this, pGrid, eGrid);
				yield return fireMultiRes.IEResolve();
				break;
			}
			default:
				Debug.Log("OtherAction happened");
				break;
			}
			nextActions = getNextActions(actions, out rem);
		}
		SetGridStates(pGrid,eGrid); // At end, make sure to set the board incase we muck up...

		//Interior function to get next action(s) feels janky, probably needs to be simplified
		//Goal is to get the next action or set of matching actions that were expanded
		List<ActionReq> getNextActions(List<ActionReq> inActions, out int remove){
			List<pAction> multiArs = new List<pAction>(){
				pAction.hellFire, pAction.fireSquare, pAction.fireRow, pAction.blockingShot,
				pAction.flare
			};
			List<ActionReq> outActions = null;
			remove = 0;
			if(inActions.Count > 0){
				outActions = new List<ActionReq>();
				outActions.Add(inActions[0]);
				inActions.RemoveAt(0);
				remove++;
			}
			else{
				return outActions;
			}
			if(multiArs.Contains(outActions[0].a)){
				while(inActions.Count > 0 && inActions[0].a == outActions[0].a && inActions[0].p == outActions[0].p){ //While we've got more matching actions from same player
					outActions.Add(inActions[0]);
					inActions.RemoveAt(0);
					remove++;
				}
			}
			return outActions;
		}
	}

	public void RXGridInput(bool pGrid, Vector2 pos, CellStruct cStruct){
		InputProcessor.instance.RXInput(pGrid, pos, cStruct);
	}

	public void RXGridHover(bool pGrid, Vector2 pos, bool enter){
		if(!enter){ // On hover Exit, just clear the hover states
			ClearSelectionState(true);
			return;
		}
		int target = pGrid ? 0 : 1; // This is janky, but we want the target to match sender if on player's grid
		ActionReq ar =  new ActionReq(0, target, actionContext, new Vector2[]{pos});
		HoverAction(ar);
	}

	public void SetCellStruct(bool pGrid, Vector2 pos, CellStruct cStruct){
		GameGrid2D g = (pGrid) ? this.playerGrid : this.enemyGrid;
		g.SetCellStruct(pos, cStruct);
	}
	
	public void ClearActions(){
		ClearSelectionState(false);
		lastActions = new List<ActionReq>{};
	}

	//Given list of actions, highlight them on the board
	public void SetActions(List<ActionReq> inputActions){
		ClearSelectionState(false);
		foreach(ActionReq ar in inputActions){
			SetCellAction(ar, false);
		}
		lastActions = inputActions;
	}

	public void HoverAction(ActionReq inputAction){
		ClearSelectionState(true);
		SetCellAction(inputAction, true);
	}

	void SetCellAction(ActionReq ar, bool hover){
		GameGrid2D g = ar.p == ar.t ? this.playerGrid : this.enemyGrid;
		if(ar.loc == null){
			return; // Return early if there's not loc of cell given. Nothing will be done
		}
		switch(ar.a){
		case pAction.fireBasic:
		case pAction.fireAgain:
		case pAction.buildWall:
		case pAction.buildOffenceTower:
		case pAction.buildDefenceTower:
		case pAction.buildIntelTower:
		case pAction.scout:
		case pAction.placeMine:
		case pAction.buildReflector:
		case pAction.firePiercing:
		case pAction.noAction:
		case pAction.towerTakeover:
			g.SetSingleSelect(hover, ar.loc[0]);
			break;
		case pAction.fireRow:
			g.SetRowSelect(hover, (int)ar.loc[0].y);
			break;
		case pAction.fireSquare:
			g.SetEmptySquareSelect(hover, ar.loc[0]);
			break;
		case pAction.placeMole:
			g.SetSquare3Select(hover, ar.loc[0], 1); // 3x3 square
			break;
		case pAction.buildDefenceGrid:
			g.SetSquare3Select(hover, ar.loc[0], 2); // 5x5 square
			break;
		case pAction.blockingShot: // these guys don't have any targeting or loc in their action
		case pAction.hellFire:
		case pAction.flare:
			break;
		default:
			Debug.Log("Unhandled pAction Type: " + ar.a.ToString());
			break;
		}
	}

	public void ClearGrids(){
		this.playerGrid.ClearCSArray();
		this.enemyGrid.ClearCSArray();
	}

	public void ClearSelectionState(bool hoveronly){
		this.playerGrid.ClearSelectionState(hoveronly);
		this.enemyGrid.ClearSelectionState(hoveronly);
	}

	public int[] GetGridSize(){
		int[] ret = {this.sizex, this.sizey};
		return ret;
	}

	public Vector2 GetGridVec2Size(){
		return new Vector2(this.sizex, this.sizey);
	}

	public CellStruct[][,] GetGridStates(){
		CellStruct [][,] ret = new CellStruct[2][,];
		ret[0] = this.playerGrid.GetCSArray(); // player's grid always idx 0
		ret[1] = this.enemyGrid.GetCSArray(); // enemy's grid always idx 1
		return ret;
	}

	void numberGrid(){
		//TBD auto number the grid
		//Should be done on InstantiateGrids
	}

	public void InstantiateGrids() {
		float width;
		float height;
		Vector3 center1;
		Vector3 center2;
		if (this.transform.localScale.x > this.transform.localScale.y){ // horizontal board
			width = (this.transform.localScale.x - this.midSpace) / 2.0f;
			height = this.transform.localScale.y;
			//Debug.Log(string.Format("W{0} > H{1}", width, height));
			center1 = this.transform.position - new Vector3(width/2.0f + this.midSpace/2.0f, 0, 0);
			center2 = this.transform.position + new Vector3(width/2.0f + this.midSpace/2.0f, 0, 0);
		}
		else{ // vertical board
			width = this.transform.localScale.x;
			height = (this.transform.localScale.y - this.midSpace) / 2.0f;
			//Debug.Log(string.Format("W{0} < H{1}", width, height));
			center1 = this.transform.position - new Vector3(0, height/2.0f + this.midSpace/2.0f, 0);
			center2 = this.transform.position + new Vector3(0, height/2.0f + this.midSpace/2.0f, 0);
		}
		//Debug.Log(string.Format("cent1 {0}, cent2 {1}", center1, center2));
		this.playerGrid = Instantiate(gridPrefab, center1, Quaternion.identity).GetComponent<GameGrid2D>();
		this.enemyGrid = Instantiate(gridPrefab, center2, Quaternion.identity).GetComponent<GameGrid2D>();
		//Debug.Log("Width/Height " + width.ToString() + " " + height.ToString());
		this.playerGrid.PlaceCells(width, height);
		int[] size = this.playerGrid.GetGridSize();
		//Debug.Log("size/size " + size[0].ToString() + " " + size[1].ToString());
		this.sizex = size[0];
		this.sizey = size[1];
		this.playerGrid.parent = this;
		this.playerGrid.playerOwnedGrid = true;
		this.enemyGrid.PlaceCells(width, height);
		this.enemyGrid.parent = this;
		this.enemyGrid.playerOwnedGrid = false;
		this.enemyGrid.Flip(); //This one's facing the player, needs to be flipped
		//Now fill out the decorative cells
		//
	}
}
