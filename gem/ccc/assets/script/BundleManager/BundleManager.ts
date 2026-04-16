import { __private, _decorator, Asset, assetManager, AssetManager, isValid } from 'cc';
import { sleep } from '../Other/sleep';

export class BundleManager
{
    private res:string="script/BundleManager/BundleManager.ts";
    private bundles:Map<string, AssetManager.Bundle> = new Map();

    public static _instance:BundleManager;
    static get Instance():BundleManager {
        if(this._instance==null)
            this._instance=new BundleManager();
        return this._instance;
    }
    
    private loadBundle(bundleRes:string) : Promise<AssetManager.Bundle> {
        return new Promise((resolve, reject) => {
            try {
                if(this.bundles.has(bundleRes))
                {
                    let bundle = this.bundles.get(bundleRes);
                    if (bundle) {
                        resolve(bundle);
                        return;
                    }
                }

                assetManager.loadBundle(bundleRes, (error,bundle) => {
                    if(error) {
                        console.warn(error.message);
                        reject();
                    }
                    else {
                        this.bundles.set(bundleRes, bundle);
                        resolve(bundle);
                    }
                });
            }
            catch (err) {
                console.warn(this.res+"下的 loadAssets 错误:"+err);
                reject();
            }    
        });
    }

    public LoadAssetsFromBundle(bundleRes:string, assetsRes:string) : Promise<Asset> {   
        return new Promise(async (resolve, reject) => {
            try {
                let bundle = await this.loadBundle(bundleRes);
                
                bundle.load(assetsRes, Asset, (error, asset) => {
                    if(error) {
                        console.warn(`loadAssets '${bundleRes}' '${assetsRes}' error:`, error.message);
                        reject();
                    }
                    else {
                        resolve(asset);
                    }
                });
            }
            catch (err) {
                console.error(this.res+"下的 loadAssetsFromBundle 错误:"+err);
                reject();
            }    
        });
    }

    public LoadAssetsFromUrl(url:string, _ext:string) : Promise<Asset> {
        return new Promise((resolve, reject) => {
            try {
                assetManager.loadRemote(url, {ext:_ext}, (err:Error|null, asset:Asset) => {
                    if (err) {
                        console.log(err.message);
                        reject();
                    } 
                    else {
                        resolve(asset);
                    }
                });
            }
            catch (err) {
                console.warn(this.res+"下的 loadAssets 错误:"+err);
                reject();
            }    
        });
    }

    /**
     * 预加载文件夹下的所有文件
     * @param _bundle 包名
     * @param _res 文件夹路径 （根目录填""）
     */
    public async PreLoadBundleDir(_bundle:string,_res:string,_callBack?:((bundleName:string,progress:number)=>void)|null, _complete?:(()=>void)|null)
    {
        return new Promise<void>(async (resolve, reject) =>
        {
            let bundle = await this.loadBundle(_bundle);
            let info = bundle.getDirWithPath(_res);
            if (info)
            {
                let n = 0;
                for (let t of info)
                {
                    let uuid = t.uuid;
                    let cachedAsset = assetManager.assets.get(uuid)
                    if (cachedAsset && isValid(cachedAsset))
                    {
                        n++;
                    }
                }
                if (n == info.length)
                {
                    if (_callBack)
                    {
                        for(let i=0; i < 10; i++) {
                            await sleep(333);
                            _callBack(_bundle, 90+i);
                        }
                    }
                    resolve();
                    return;
                }
            }

            bundle.preloadDir(_res, null, (finished, total, item) =>
            {
                if (_callBack)
                {
                    _callBack(_bundle, Math.floor(finished / total * 100));
                }
            }, async (err, data) =>
            {
                if (err)
                {
                    console.warn("预下载 ",bundle,"/",_res," 错误 ",err);
                    resolve();
                }
                else
                {
                    if(_complete)
                    {
                        _complete();
                    }
                    resolve();
                }
            });
        })
    }
}


