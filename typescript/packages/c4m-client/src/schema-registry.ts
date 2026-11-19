import type { Api, CmsTypeDescriptor, CreateCmsTypeDescriptorRequest } from "./index.js";

// The shape accepted by c4m-schema's syncSchemas. The client registry also
// exposes the registered IDs, which authors need when saving blocks and menus.
type TypeRegistry = {
  readonly list: () => Promise<readonly CmsTypeDescriptor[]>;
  readonly create: (descriptor: CreateCmsTypeDescriptorRequest) => Promise<void>;
};

const blockTypeRegistry = <SecurityData>(api: Api<SecurityData>): TypeRegistry => ({
  list: async () => {
    const items: CmsTypeDescriptor[] = [];
    let offset: number | null | undefined = 0;
    while (offset !== null && offset !== undefined) {
      const { data } = await api.blockTypes.listBlockTypes({ offset, limit: 50 });
      items.push(...data.items);
      offset = data.nextOffset;
    }
    return items;
  },
  create: async (descriptor) => {
    await api.blockTypes.createBlockType(descriptor);
  },
});

const menuActionTypeRegistry = <SecurityData>(api: Api<SecurityData>): TypeRegistry => ({
  list: async () => (await api.menuActionTypes.listMenuActionTypes()).data.items,
  create: async (descriptor) => {
    await api.menuActionTypes.createMenuActionType(descriptor);
  },
});

export { blockTypeRegistry, menuActionTypeRegistry };
export type { TypeRegistry };
