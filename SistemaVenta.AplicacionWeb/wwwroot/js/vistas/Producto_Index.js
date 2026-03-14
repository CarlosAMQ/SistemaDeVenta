const MODELO_BASE = {
    idProducto: 0,
    codigoBarra: "",
    marca: "",
    descripcion: "",
    idCategoria: 0,
    stock: 0,
    urlImagen: "",
    precio: 0.0,
    esActivo: 1,
    medidaEmpresa: "0",
    medidaSat: "0",
    claveProductoSat: "0",
    objetoImpuesto: "0",
    impuesto: "0",
    factorImpuesto: "0",
    valorImpuesto: 0.0,
    tipoImpuesto: "0",
    descuento: 0.0
};

const unidadDeMedidaMap = new Map();
unidadDeMedidaMap.set("H87", "Pieza");
unidadDeMedidaMap.set("KGM", "Kilogramo");
unidadDeMedidaMap.set("LTR", "Litro");
unidadDeMedidaMap.set("MTR", "Metro");
unidadDeMedidaMap.set("GRM", "Gramo");
unidadDeMedidaMap.set("BG", "Bolsa");
unidadDeMedidaMap.set("XBX", "Caja");
unidadDeMedidaMap.set("RO", "Rollo");

let tablaData;
let filaSeleccionada;

$(document).ready(function () {

    // Cargar categorías
    fetch("/Categoria/Lista")
        .then(response => response.ok ? response.json() : Promise.reject(response))
        .then(responseJson => {

            // Opción "Seleccionar" por defecto
            $("#cboCategoria").append(
                $("<option>").val(0).text("Seleccionar")
            );

            if (responseJson.data.length > 0) {
                responseJson.data.forEach(item => {
                    $("#cboCategoria").append(
                        $("<option>").val(item.idCategoria).text(item.descripcion)
                    );
                });
            }
        });

    // Inicializar DataTable
    tablaData = $('#tbdata').DataTable({
        responsive: true,
        ajax: {
            url: '/Producto/Lista',
            type: "GET",
            datatype: "json"
        },
        columns: [
            { data: "idProducto", visible: false },
            {
                data: "urlImagen",
                render: function (data) {
                    return `<img style="height:60px" src="${data}" class="rounded mx-auto d-block"/>`;
                }
            },
            { data: "codigoBarra" },
            { data: "marca" },
            { data: "descripcion" },
            { data: "nombreCategoria" },
            { data: "stock" },
            { data: "precio" },
            {
                data: "esActivo",
                render: function (data) {
                    if (data == 1)
                        return '<span class="badge badge-info">Activo</span>';
                    else
                        return '<span class="badge badge-danger">No Activo</span>';
                }
            },
            {
                defaultContent:
                    '<button class="btn btn-primary btn-editar btn-sm mr-2"><i class="fas fa-pencil-alt"></i></button>' +
                    '<button class="btn btn-danger btn-eliminar btn-sm"><i class="fas fa-trash-alt"></i></button>',
                orderable: false,
                searchable: false,
                width: "80px"
            }
        ],
        order: [[0, "desc"]],
        dom: "Bfrtip",
        buttons: [
            {
                text: 'Exportar Excel',
                extend: 'excelHtml5',
                filename: 'Reporte Productos',
                exportOptions: { columns: [2, 3, 4, 5, 6] }
            },
            'pageLength'
        ],
        language: { url: "https://cdn.datatables.net/plug-ins/1.11.5/i18n/es-ES.json" }
    });

});

// Mostrar modal
function mostrarModal(modelo) {

    const esNuevo = !modelo || !modelo.idProducto || modelo.idProducto === 0;

    // Si es nuevo, clonar el modelo base limpio
    if (esNuevo) {
        modelo = structuredClone(MODELO_BASE);
    }

    $("#txtId").val(modelo.idProducto);
    $("#txtCodigoBarra").val(modelo.codigoBarra);
    $("#txtMarca").val(modelo.marca);
    $("#txtDescripcion").val(modelo.descripcion);

    // Categoría
    $("#cboCategoria").val(esNuevo ? "0" : String(modelo.idCategoria ?? "0"));

    $("#txtStock").val(modelo.stock);
    $("#txtPrecio").val(modelo.precio);
    $("#cboEstado").val(modelo.esActivo ?? 1);

    $("#txtImagen").val("");
    $("#imgProducto").attr("src", esNuevo ? "" : (modelo.urlImagen || ""));

    // Campos nuevos (combos) → si es nuevo, todos a "0"
    $("#cboMedidaEmpresa").val(esNuevo ? "0" : (modelo.medidaEmpresa ?? "0"));
    $("#cboMedidaSat").val(esNuevo ? "0" : (modelo.medidaSat ?? "0"));
    $("#cboClaveProductoSat").val(esNuevo ? "0" : (modelo.claveProductoSat ?? "0"));
    $("#cboObjetoImpuesto").val(esNuevo ? "0" : (modelo.objetoImpuesto ?? "0"));
    $("#cboImpuesto").val(esNuevo ? "0" : (modelo.impuesto ?? "0"));
    $("#cboFactorImpuesto").val(esNuevo ? "0" : (modelo.factorImpuesto ?? "0"));
    $("#decValorImpuesto").val(esNuevo ? "" : (modelo.valorImpuesto ?? ""));
    $("#cboTipoImpuesto").val(esNuevo ? "0" : (modelo.tipoImpuesto ?? "0"));
    $("#decDescuento").val(esNuevo ? "" : (modelo.descuento ?? ""));

    $("#modalData").modal("show");
}

$("#btnNuevo").click(function () {
    mostrarModal(null);
});

$("#btnGuardar").click(function () {

    const camposRequeridos = [
        // ===== Inputs obligatorios (en el orden que quieres validar) =====
        { selector: "#txtCodigoBarra", tipo: "input", nombre: "Código Barra" },
        { selector: "#txtMarca", tipo: "input", nombre: "Marca" },
        { selector: "#txtDescripcion", tipo: "input", nombre: "Descripción" },
        { selector: "#txtStock", tipo: "input", nombre: "Stock" },
        { selector: "#txtPrecio", tipo: "input", nombre: "Precio Unitario" },
        { selector: "#decValorImpuesto", tipo: "input", nombre: "Valor Impuesto" },

        // ===== Selects obligatorios =====
        { selector: "#cboMedidaEmpresa", tipo: "select", nombre: "Medida de Empresa" },
        { selector: "#cboMedidaSat", tipo: "select", nombre: "Medida del SAT" },
        { selector: "#cboCategoria", tipo: "select", nombre: "Categoría" },
        { selector: "#cboClaveProductoSat", tipo: "select", nombre: "Clave Producto SAT" },
        { selector: "#cboObjetoImpuesto", tipo: "select", nombre: "Objeto del Impuesto" },
        { selector: "#cboImpuesto", tipo: "select", nombre: "Impuesto" },
        { selector: "#cboFactorImpuesto", tipo: "select", nombre: "Factor del Impuesto" },
        { selector: "#cboTipoImpuesto", tipo: "select", nombre: "Tipo de Impuesto" }
    ];

    for (const c of camposRequeridos) {
        const $campo = $(c.selector);
        const val = $campo.val();

        if (c.tipo === "input") {
            if (!val || val.trim() === "") {
                toastr.warning("", `Debe completar el campo "${c.nombre}"`);
                $campo.focus();
                return;
            }
        } else if (c.tipo === "select") {
            if (val === "0" || val === null || val === "") {
                toastr.warning("", `Debe seleccionar "${c.nombre}"`);
                $campo.focus();
                return;
            }
        }
    }

    // Si pasa todas las validaciones, ya armamos el modelo

    const modelo = structuredClone(MODELO_BASE);
    modelo.idProducto = parseInt($("#txtId").val());
    modelo.codigoBarra = $("#txtCodigoBarra").val();
    modelo.marca = $("#txtMarca").val();
    modelo.descripcion = $("#txtDescripcion").val();
    modelo.idCategoria = $("#cboCategoria").val();
    modelo.stock = $("#txtStock").val();
    modelo.precio = $("#txtPrecio").val();
    modelo.esActivo = $("#cboEstado").val();

    // Campos adicionales
    modelo.medidaEmpresa = $("#cboMedidaEmpresa").val();
    modelo.medidaSat = $("#cboMedidaSat").val();
    modelo.unidadDeMedida = unidadDeMedidaMap.get($("#cboMedidaSat").val()) || "";
    modelo.claveProductoSat = $("#cboClaveProductoSat").val();
    modelo.objetoImpuesto = $("#cboObjetoImpuesto").val();
    modelo.impuesto = $("#cboImpuesto").val();
    modelo.factorImpuesto = $("#cboFactorImpuesto").val();
    modelo.valorImpuesto = $("#decValorImpuesto").val();
    modelo.tipoImpuesto = $("#cboTipoImpuesto").val();
    modelo.descuento = $("#decDescuento").val();

    const inputFoto = document.getElementById("txtImagen");
    const formData = new FormData();
    formData.append("imagen", inputFoto.files[0]);
    formData.append("modelo", JSON.stringify(modelo));

    $("#modalData").find("div.modal-content").LoadingOverlay("show");

    if (modelo.idProducto == 0) {
        // Crear
        fetch("/Producto/Crear", {
            method: "POST",
            body: formData
        })
            .then(response => {
                $("#modalData").find("div.modal-content").LoadingOverlay("hide");
                return response.ok ? response.json() : Promise.reject(response);
            })
            .then(responseJson => {
                if (responseJson.estado) {
                    tablaData.row.add(responseJson.objeto).draw(false);
                    $("#modalData").modal("hide");
                    swal("Listo!", "El producto fue creado", "success");
                } else {
                    swal("Lo sentimos", responseJson.mensaje, "error");
                }
            });
    } else {
        // Editar
        fetch("/Producto/Editar", {
            method: "PUT",
            body: formData
        })
            .then(response => {
                $("#modalData").find("div.modal-content").LoadingOverlay("hide");
                return response.ok ? response.json() : Promise.reject(response);
            })
            .then(responseJson => {
                if (responseJson.estado) {
                    tablaData.row(filaSeleccionada).data(responseJson.objeto).draw(false);
                    filaSeleccionada = null;
                    $("#modalData").modal("hide");
                    swal("Listo!", "El producto fue modificado", "success");
                } else {
                    swal("Lo sentimos", responseJson.mensaje, "error");
                }
            });
    }
});


// Botón editar
$("#tbdata tbody").on("click", ".btn-editar", function () {
    filaSeleccionada = $(this).closest("tr").hasClass("child")
        ? $(this).closest("tr").prev()
        : $(this).closest("tr");

    const data = tablaData.row(filaSeleccionada).data();
    mostrarModal(data);
});

// Botón eliminar
$("#tbdata tbody").on("click", ".btn-eliminar", function () {

    let fila = $(this).closest("tr").hasClass("child")
        ? $(this).closest("tr").prev()
        : $(this).closest("tr");

    const data = tablaData.row(fila).data();

    swal({
        title: "¿Está seguro?",
        text: `Eliminar el producto "${data.descripcion}"`,
        type: "warning",
        showCancelButton: true,
        confirmButtonClass: "btn-danger",
        confirmButtonText: "Sí, eliminar",
        cancelButtonText: "Cancelar",
        closeOnConfirm: false,
        closeOnCancel: true
    }, function (respuesta) {
        if (respuesta) {

            $(".showSweetAlert").LoadingOverlay("show");

            fetch(`/Producto/Eliminar?IdProducto=${data.idProducto}`, {
                method: "DELETE"
            })
                .then(response => {
                    $(".showSweetAlert").LoadingOverlay("hide");
                    return response.ok ? response.json() : Promise.reject(response);
                })
                .then(responseJson => {
                    if (responseJson.estado) {
                        tablaData.row(fila).remove().draw();
                        swal("Listo!", "El producto fue eliminado", "success");
                    } else {
                        swal("Lo sentimos", responseJson.mensaje, "error");
                    }
                });
        }
    });
});
